export type Availability = "NotSpecified" | "Available" | "Limited" | "Unavailable";

export type PortfolioItem = { title: string; description: string; url: string };

export type UpdateFreelancerProfileRequest = {
  headline: string;
  about: string;
  location: string;
  yearsOfExperience: number | null;
  availability: Availability;
  skills: string[];
  languages: string[];
  portfolioItems: PortfolioItem[];
};

export type FreelancerProfile = UpdateFreelancerProfileRequest & {
  userId: string;
  fullName: string;
  memberSince: string;
  walletVerified: boolean;
  completedProjectCount: number;
  completedProjects: { title: string; milestoneCount: number; completedAt: string | null }[];
  updatedAt: string | null;
  hasResume: boolean;
  resumeFileSize: number | null;
  resumeUploadedAt: string | null;
};

export type ResumeMetadata = Pick<FreelancerProfile, "hasResume" | "resumeFileSize" | "resumeUploadedAt">;

export const availabilityLabels: Record<Availability, string> = {
  NotSpecified: "Availability not specified",
  Available: "Open to new projects",
  Limited: "Limited availability",
  Unavailable: "Not taking new projects",
};
