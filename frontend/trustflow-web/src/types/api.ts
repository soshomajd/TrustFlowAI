export type ApiProblemDetails = {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  traceId?: string;
  message?: string;
  errors?: Record<string, string[]>;

  // Machine-readable reason sent by the auth endpoints, such as
  // "email_not_verified" or "resend_throttled".
  code?: string;
  email?: string;
  retryAfterSeconds?: number;
};
