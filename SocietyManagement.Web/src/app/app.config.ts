import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, provideAppInitializer, inject, provideZoneChangeDetection } from '@angular/core';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { provideCharts, withDefaultRegisterables } from 'ng2-charts';
import { routes } from './app.routes';
import { errorInterceptor } from './core/interceptors/error.interceptor';
import { jwtInterceptor } from './core/interceptors/jwt.interceptor';
import { loadingInterceptor } from './core/interceptors/loading.interceptor';
import { AuthService } from './core/services/auth.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes, withComponentInputBinding()),
    provideAnimationsAsync(),
    provideHttpClient(withInterceptors([jwtInterceptor, errorInterceptor, loadingInterceptor])),
    // Registers every Chart.js controller/element (bar, doughnut, pie, line, ...)
    // that <canvas baseChart> can use anywhere in the app — ng2-charts doesn't
    // do this automatically, and without it every chart type but the first one
    // Chart.js happens to lazily register throws "'<type>' is not a registered
    // controller" at render time.
    provideCharts(withDefaultRegisterables()),
    // Rehydrates the session (GET /auth/me) before the app renders, so a page
    // refresh doesn't flash the login screen for an already-authenticated user.
    provideAppInitializer(() => {
      const auth = inject(AuthService);
      return auth.restoreSession();
    })
  ]
};
