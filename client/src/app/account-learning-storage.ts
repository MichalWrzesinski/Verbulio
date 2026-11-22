export const accountLearningStoragePrefix = 'verbulio.account-learning';

export function clearAccountLearningStorage(): void {
  try {
    sessionStorage.removeItem(accountLearningStoragePrefix + '.active-session');
    sessionStorage.removeItem(accountLearningStoragePrefix + '.feedback');
  } catch {
    // The browser can make tab storage unavailable.
  }
}
