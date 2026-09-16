import { FreelancerProfileScreen } from "@/features/freelancers/components/freelancer-profile-screen";

export default async function FreelancerProfilePage({ params }: { params: Promise<{ freelancerId: string }> }) {
  const { freelancerId } = await params;
  return <FreelancerProfileScreen freelancerId={freelancerId} />;
}
