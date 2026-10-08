import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AuthService } from './auth.service';
import { authInterceptor } from './auth.interceptor';

describe('authInterceptor', () => {
  let httpClient: HttpClient;
  let httpMock: HttpTestingController;
  let authService: AuthService;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting()]
    });
    httpClient = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    authService = TestBed.inject(AuthService);
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  it('attaches no Authorization header when there is no current user', () => {
    httpClient.get('/api/claims').subscribe();

    const req = httpMock.expectOne('/api/claims');
    expect(req.request.headers.has('Authorization')).toBeFalse();
    req.flush({});
  });

  it('attaches the Bearer token automatically once a user is authenticated', async () => {
    const loginPromise = authService.login('manager1');
    httpMock
      .expectOne((request) => request.url.endsWith('/auth/login'))
      .flush({ token: 'fake-jwt', userId: 'u1', username: 'manager1', displayName: 'M', role: 'Manager', organizationId: 'o1' });
    await loginPromise;

    httpClient.get('/api/claims').subscribe();

    const req = httpMock.expectOne('/api/claims');
    expect(req.request.headers.get('Authorization')).toBe('Bearer fake-jwt');
    req.flush({});
  });
});
