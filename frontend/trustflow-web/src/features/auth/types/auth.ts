export type AuthRole = "Client" | "Freelancer" | "Admin";

export type AuthUser = {
  id: string;
  fullName: string;
  email: string | null;
  roles: AuthRole[];
};

export type AuthSessionResponse = {
  readOnly?: boolean;
  localReview?: boolean;
  accessToken: string;
  expiresAtUtc: string;
  user: AuthUser;
};

export type LoginRequest = {
  email: string;
  password: string;
};

export type RegisterRequest = {
  fullName: string;
  email: string;
  password: string;
  role: Exclude<AuthRole, "Admin">;
};

export type RegisterResponse = {
  id: string;
  fullName: string;
  email: string;
  role: Exclude<AuthRole, "Admin">;
  createdAt: string;

  // The account cannot sign in until the mailed code is entered.
  verificationRequired: boolean;
  verificationEmailSent: boolean;
};

export type VerifyEmailRequest = {
  email: string;
  code: string;
};

export type ResetPasswordRequest = {
  email: string;
  code: string;
  newPassword: string;
};

export type MessageResponse = {
  message: string;
};

