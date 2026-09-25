import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree, provideRouter } from '@angular/router';
import { Observable, firstValueFrom } from 'rxjs';

import { adminGuard } from './admin.guard';

describe('adminGuard', () => {
  let http: HttpTestingController;
  let router: Router;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
  });

  afterEach(() => http.verify());

  function run(): Promise<boolean | UrlTree> {
    const result = TestBed.runInInjectionContext(() =>
      adminGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot),
    ) as Observable<boolean | UrlTree>;
    return firstValueFrom(result);
  }

  it('FE-08 lets the navigation through when /api/admin/me answers 204', async () => {
    const pending = run();
    http.expectOne('/api/admin/me').flush(null, { status: 204, statusText: 'No Content' });

    expect(await pending).toBeTrue();
  });

  it('FE-08 sends an anonymous visitor to the sign-in gate on 401', async () => {
    const pending = run();
    http.expectOne('/api/admin/me').flush(null, { status: 401, statusText: 'Unauthorized' });

    const result = await pending;
    expect(result instanceof UrlTree).toBeTrue();
    expect(router.serializeUrl(result as UrlTree)).toBe('/admin/login');
  });
});
