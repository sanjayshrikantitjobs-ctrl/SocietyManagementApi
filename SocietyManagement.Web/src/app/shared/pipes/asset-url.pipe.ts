import { Pipe, PipeTransform } from '@angular/core';
import { environment } from '../../../environments/environment';

/** LocalFileStorageService (API) returns every uploaded file's URL as a
 * relative path like "/uploads/festivals/xyz.jpg" — correct for the API to
 * store, but the Angular app and API are on different origins (dev: SPA on
 * :4200, API on :7001; prod: apiUrl may still differ per deployment), so a
 * relative path bound straight into <img src> or a CSS url(...) resolves
 * against the SPA's own origin and 404s. This resolves it against the API's
 * origin (environment.apiUrl minus its trailing "/api") at render time only —
 * the relative path is still what's stored/submitted everywhere else. */
export function resolveAssetUrl(url: string | null | undefined): string {
  if (!url) return '';
  if (/^(https?:)?\/\//i.test(url) || url.startsWith('data:') || url.startsWith('blob:')) return url;

  const apiOrigin = environment.apiUrl.replace(/\/api\/?$/, '');
  return apiOrigin + url;
}

@Pipe({ name: 'assetUrl', standalone: true, pure: true })
export class AssetUrlPipe implements PipeTransform {
  transform(url: string | null | undefined): string {
    return resolveAssetUrl(url);
  }
}
