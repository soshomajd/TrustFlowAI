export type ClientAccountType = "Individual" | "Company";
export type ClientVerificationStatus = "NotSubmitted" | "Pending" | "Verified" | "Rejected";
export type CompanySize = "NotSpecified" | "JustMe" | "2-10" | "11-50" | "51-200" | "201Plus";

export type UpdateClientProfileRequest = {
  accountType: ClientAccountType;
  companyName: string;
  overview: string;
  industry: string;
  location: string;
  website: string;
  companySize: CompanySize;
};

export type ClientProfile = UpdateClientProfileRequest & {
  userId: string;
  displayName: string;
  memberSince: string;
  walletVerified: boolean;
  identityVerified: boolean;
  identityVerifiedAt: string | null;
  verificationStatus: ClientVerificationStatus;
  verificationSubmittedAt: string | null;
  verificationRejectionReason: string;
  projectsPosted: number;
  activeProjects: number;
  completedProjects: number;
  totalHires: number;
  updatedAt: string | null;
};

export type ClientVerificationAdminItem = {
  clientId: string;
  displayName: string;
  email: string;
  accountType: ClientAccountType;
  legalName: string;
  countryCode: string;
  registrationNumber: string;
  documentFileSize: number;
  submittedAt: string;
};

export const companySizeLabels: Record<CompanySize, string> = {
  NotSpecified: "Not specified",
  JustMe: "Just me",
  "2-10": "2-10 people",
  "11-50": "11-50 people",
  "51-200": "51-200 people",
  "201Plus": "201+ people",
};
