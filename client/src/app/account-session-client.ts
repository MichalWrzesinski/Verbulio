import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { clearAccountLearningStorage } from './account-learning-storage';

export interface Account {
  id: string;
  startingLevel: string;
}

export const accountEndpoint = '/api/v1/local-account';
export const accountRequestOptions = {
  headers: { 'X-Verbulio-Client': 'local-learning' },
  timeout: 10_000,
};

@Injectable({ providedIn: 'root' })
export class AccountSessionClient {
  private readonly http = inject(HttpClient);

  async read(): Promise<Account | null> {
    try {
      return await firstValueFrom(
        this.http.get<Account>(`${accountEndpoint}/session`, accountRequestOptions),
      );
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 401) return null;
      throw error;
    }
  }

  async logout(): Promise<Account | null> {
    let remaining: Account | null = null;
    try {
      await firstValueFrom(
        this.http.post(`${accountEndpoint}/logout`, null, accountRequestOptions),
      );
    } catch {
      remaining = await this.read();
    }
    if (!remaining) clearAccountLearningStorage();
    return remaining;
  }
}
