export type AuthRole = "Client" | "Freelancer" | "Admin";

export type AuthUser = {
  id: string;
  fullName: string;
  email: string | null;
  roles: AuthRole[];
};

export type AuthSessionResponse = {
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

export type GoogleSignInRequest = {
  credential: string;
  role?: Exclude<AuthRole, "Admin">;
};

// Returned as HTTP 409 the first time a Google account signs in, because a
// TrustFlow account cannot exist without a role.
export type GoogleRoleRequiredResponse = {
  code: "role_required";
  message: string;
  email: string;
  fullName: string | null;
};
