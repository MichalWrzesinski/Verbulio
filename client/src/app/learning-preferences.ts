import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';

interface LearningPreference {
  autoAdvance: boolean;
}

@Injectable({ providedIn: 'root' })
export class LearningPreferences {
  private readonly http = inject(HttpClient);
  private readonly url = '/api/v1/local-account/settings/learning';

  read() {
    return this.http.get<LearningPreference>(this.url);
  }

  save(autoAdvance: boolean) {
    return this.http.put<LearningPreference>(
      this.url,
      { autoAdvance },
      { headers: { 'X-Verbulio-Client': 'local-learning' } },
    );
  }
}
