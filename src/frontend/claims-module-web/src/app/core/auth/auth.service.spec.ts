import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';
import { LoginResponse } from './models/authenticated-user.model';

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;

  const response: LoginResponse = {
    token: 'fake-jwt',
    userId: 'user-1',
    username: 'manager1',
    displayName: 'Mika Manager',
    role: 'Manager',
    organizationId: 'org-1'
  };

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({ imports: [HttpClientTestingModule] });
    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  it('starts unauthenticated with no stored session', () => {
    expect(service.isAuthenticated()).toBeFalse();
    expect(service.currentUser()).toBeNull();
  });

  it('login posts the username and stores the returned user on success', async () => {
    const loginPromise = service.login('manager1');

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/auth/login`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ username: 'manager1' });
    req.flush(response);

    await loginPromise;

    expect(service.isAuthenticated()).toBeTrue();
    expect(service.currentUser()?.token).toBe('fake-jwt');
    expect(service.currentUser()?.role).toBe('Manager');
  });

  it('logout clears the current user', async () => {
    const loginPromise = service.login('manager1');
    httpMock.expectOne(`${environment.apiBaseUrl}/auth/login`).flush(response);
    await loginPromise;

    service.logout();

    expect(service.isAuthenticated()).toBeFalse();
    expect(service.currentUser()).toBeNull();
  });
});
