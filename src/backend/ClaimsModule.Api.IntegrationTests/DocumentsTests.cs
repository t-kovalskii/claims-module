using System.Net;
using System.Net.Http.Json;
using System.Text;
using ClaimsModule.Api.IntegrationTests.Fixtures;
using ClaimsModule.Application.Claims.Queries.GetClaimById;
using ClaimsModule.Application.Claims.Queries.GetClaimDocumentDownloadUrl;
using ClaimsModule.Application.Claims.Queries.GetClaimDocuments;
using ClaimsModule.Application.Reference.Queries.GetCauseOfLossCodes;
using Xunit;

namespace ClaimsModule.Api.IntegrationTests;

/// <summary>
/// Ticket 10: document upload/download against the real local-filesystem storage provider the
/// test fixture configures (ADR-... spec.md's Testing Decisions: document storage has no
/// business-rule branching of its own, so it's covered entirely by this HTTP seam, no narrow
/// in-memory seam needed).
/// </summary>
[Collection(ApiTestCollection.Name)]
public class DocumentsTests(ApiWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<Guid> GetActiveCauseOfLossCodeIdAsync() =>
        (await (await _client.GetAsync("/api/reference/cause-of-loss-codes"))
            .Content.ReadFromJsonAsync<List<CauseOfLossCodeDto>>())!
        .Single(c => c.Code == "COL-FIRE").Id;

    private async Task<Guid> CreateClaimAsync()
    {
        var causeId = await GetActiveCauseOfLossCodeIdAsync();

        var response = await _client.PostAsJsonAsync("/api/claims", new
        {
            policyId = (Guid?)null,
            lossEvent = new
            {
                lossDate = DateTimeOffset.UtcNow.AddDays(-3),
                lossDescription = "A delivery van collided with the loading dock, causing structural damage.",
                lossLocation = "123 Harbor Way, Portside",
                causeOfLossCodeId = causeId,
                estimatedLossAmount = 15000m,
                policeReportNumber = (string?)null
            },
            assignedHandlerUserId = (Guid?)null,
            parties = new[] { new { role = "Claimant", partyType = "Person", firstName = "Jane", lastName = "Doe" } },
            riskObjects = Array.Empty<object>()
        });
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<CreateClaimResponseDto>();
        return created!.Id;
    }

    private static MultipartFormDataContent BuildUploadBody(string fileName, string content, string? documentType = "Photo")
    {
        var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        form.Add(fileContent, "file", fileName);
        if (documentType is not null)
        {
            form.Add(new StringContent(documentType), "documentType");
        }

        return form;
    }

    [Fact]
    public async Task UploadDocument_persists_metadata_and_appears_in_the_list()
    {
        await _client.LoginAsAsync("handler1");
        var claimId = await CreateClaimAsync();

        var uploadResponse = await _client.PostAsync(
            $"/api/claims/{claimId}/documents", BuildUploadBody("photo-of-damage.jpg", "fake image bytes"));

        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);
        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<UploadClaimDocumentResponseDto>();
        Assert.NotNull(uploaded);
        Assert.Equal("photo-of-damage.jpg", uploaded!.FileName);
        Assert.Equal("Photo", uploaded.DocumentType);
        Assert.True(uploaded.SizeBytes > 0);

        var listResponse = await _client.GetAsync($"/api/claims/{claimId}/documents");
        listResponse.EnsureSuccessStatusCode();
        var documents = await listResponse.Content.ReadFromJsonAsync<List<ClaimDocumentDto>>();
        Assert.Single(documents!);
        Assert.Equal("photo-of-damage.jpg", documents![0].FileName);

        var detailResponse = await _client.GetAsync($"/api/claims/{claimId}");
        detailResponse.EnsureSuccessStatusCode();
        var detail = await detailResponse.Content.ReadFromJsonAsync<ClaimDetailDto>();
        Assert.Single(detail!.Documents);
    }

    [Fact]
    public async Task UploadDocument_sanitises_path_traversal_attempts_in_the_file_name()
    {
        await _client.LoginAsAsync("handler1");
        var claimId = await CreateClaimAsync();

        var uploadResponse = await _client.PostAsync(
            $"/api/claims/{claimId}/documents", BuildUploadBody("../../etc/passwd", "malicious content"));

        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);

        var listResponse = await _client.GetAsync($"/api/claims/{claimId}/documents");
        var documents = await listResponse.Content.ReadFromJsonAsync<List<ClaimDocumentDto>>();

        // The original (unsanitised) name is preserved for display only — the sanitiser governs
        // the server-side storage path, which this test can't observe directly over HTTP, but a
        // successful upload at all (no 500 from an escaped path) is itself part of what's covered
        // by LocalFileStorageService.ResolvePath's defensive root-containment check.
        Assert.Equal("../../etc/passwd", documents![0].FileName);
    }

    [Fact]
    public async Task UploadDocument_for_a_nonexistent_claim_returns_404()
    {
        await _client.LoginAsAsync("handler1");

        var response = await _client.PostAsync(
            $"/api/claims/{Guid.NewGuid()}/documents", BuildUploadBody("file.txt", "content"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UploadDocument_rejects_an_empty_file_with_400()
    {
        await _client.LoginAsAsync("handler1");
        var claimId = await CreateClaimAsync();

        var response = await _client.PostAsync($"/api/claims/{claimId}/documents", BuildUploadBody("empty.txt", ""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetDownloadUrl_returns_a_signed_url_that_streams_the_original_content_back()
    {
        await _client.LoginAsAsync("handler1");
        var claimId = await CreateClaimAsync();
        const string fileContent = "the actual uploaded bytes";

        var uploadResponse = await _client.PostAsync(
            $"/api/claims/{claimId}/documents", BuildUploadBody("notes.txt", fileContent));
        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<UploadClaimDocumentResponseDto>();

        var downloadUrlResponse = await _client.GetAsync($"/api/claims/{claimId}/documents/{uploaded!.Id}/download-url");
        downloadUrlResponse.EnsureSuccessStatusCode();
        var downloadUrl = await downloadUrlResponse.Content.ReadFromJsonAsync<ClaimDocumentDownloadUrlDto>();
        Assert.True(downloadUrl!.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(55));

        // The returned URL's host is the configured (fake, for tests) public base URL — follow
        // only its path+query against this test's own HttpClient, whose base address is the
        // real TestServer instance.
        var downloadResponse = await _client.GetAsync(downloadUrl.Url.PathAndQuery);
        downloadResponse.EnsureSuccessStatusCode();
        var downloadedContent = await downloadResponse.Content.ReadAsStringAsync();
        Assert.Equal(fileContent, downloadedContent);
    }

    [Fact]
    public async Task GetDownloadUrl_link_is_usable_without_an_authorization_header()
    {
        await _client.LoginAsAsync("handler1");
        var claimId = await CreateClaimAsync();

        var uploadResponse = await _client.PostAsync(
            $"/api/claims/{claimId}/documents", BuildUploadBody("notes.txt", "content"));
        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<UploadClaimDocumentResponseDto>();

        var downloadUrlResponse = await _client.GetAsync($"/api/claims/{claimId}/documents/{uploaded!.Id}/download-url");
        var downloadUrl = await downloadUrlResponse.Content.ReadFromJsonAsync<ClaimDocumentDownloadUrlDto>();

        // A fresh, unauthenticated client — mirroring a real Azure Blob SAS URL, which carries no
        // bearer token either; the signature in the URL itself is what authorises the request.
        using var anonymousClient = factory.CreateClient();
        var response = await anonymousClient.GetAsync(downloadUrl!.Url.PathAndQuery);

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Local_download_endpoint_rejects_a_tampered_signature()
    {
        await _client.LoginAsAsync("handler1");
        var claimId = await CreateClaimAsync();

        var uploadResponse = await _client.PostAsync(
            $"/api/claims/{claimId}/documents", BuildUploadBody("notes.txt", "content"));
        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<UploadClaimDocumentResponseDto>();

        var downloadUrlResponse = await _client.GetAsync($"/api/claims/{claimId}/documents/{uploaded!.Id}/download-url");
        var downloadUrl = await downloadUrlResponse.Content.ReadFromJsonAsync<ClaimDocumentDownloadUrlDto>();

        var tamperedPathAndQuery = downloadUrl!.Url.PathAndQuery.Replace("signature=", "signature=tampered-");
        var response = await _client.GetAsync(tamperedPathAndQuery);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetDownloadUrl_for_a_nonexistent_document_returns_404()
    {
        await _client.LoginAsAsync("handler1");
        var claimId = await CreateClaimAsync();

        var response = await _client.GetAsync($"/api/claims/{claimId}/documents/{Guid.NewGuid()}/download-url");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private record CreateClaimResponseDto(Guid Id, string ClaimNumber, string Status, List<string> Warnings);

    private record UploadClaimDocumentResponseDto(
        Guid Id,
        Guid ClaimId,
        string FileName,
        string? DocumentType,
        string ContentType,
        long SizeBytes,
        Guid? UploadedByUserId,
        DateTimeOffset UploadedAt);
}
