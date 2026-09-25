import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';

import { AdminLoginComponent } from './admin-login.component';

describe('AdminLoginComponent', () => {
  let fixture: ComponentFixture<AdminLoginComponent>;
  let http: HttpTestingController;
  let router: Router;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AdminLoginComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    spyOn(router, 'navigateByUrl').and.resolveTo(true);

    fixture = TestBed.createComponent(AdminLoginComponent);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  it('submit is disabled until a password is typed', () => {
    const button: HTMLButtonElement = fixture.nativeElement.querySelector('button[type="submit"]');
    expect(button.disabled).toBeTrue();

    fixture.componentInstance.form.controls.password.setValue('x');
    fixture.detectChanges();

    expect(button.disabled).toBeFalse();
  });

  it('posts the password and navigates to /admin on success', () => {
    fixture.componentInstance.form.controls.password.setValue('change-me-locally');
    fixture.componentInstance.submit();

    const req = http.expectOne('/api/admin/login');
    expect(req.request.body).toEqual({ password: 'change-me-locally' });
    req.flush(null, { status: 204, statusText: 'No Content' });

    expect(router.navigateByUrl).toHaveBeenCalledWith('/admin');
  });

  it('a 401 shows the wrong-password message, clears the field, and does not navigate', () => {
    fixture.componentInstance.form.controls.password.setValue('wrong');
    fixture.componentInstance.submit();
    http.expectOne('/api/admin/login').flush(null, { status: 401, statusText: 'Unauthorized' });
    fixture.detectChanges();

    const alert: HTMLElement = fixture.nativeElement.querySelector('[role="alert"]');
    expect(alert.textContent).toContain('isn’t right');
    expect(fixture.componentInstance.form.controls.password.value).toBe('');
    expect(router.navigateByUrl).not.toHaveBeenCalled();
  });
});
