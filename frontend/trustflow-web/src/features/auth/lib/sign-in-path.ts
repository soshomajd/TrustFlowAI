export function isAdminDashboardPath(path: string): boolean {
  return /^\/dashboard\/(admin|review)(\/[A-Za-z0-9_-]+)*$/.test(path);
}

export function getAdminReturnPath(path?: string): string {
  return path && isAdminDashboardPath(path) ? path : "/dashboard/admin";
}

export function getSignInPath(path: string): string {
  const signIn = isAdminDashboardPath(path) ? "/admin/login" : "/login";
  return `${signIn}?next=${encodeURIComponent(path)}`;
}
