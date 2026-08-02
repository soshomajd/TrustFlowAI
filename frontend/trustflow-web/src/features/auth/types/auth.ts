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
};
