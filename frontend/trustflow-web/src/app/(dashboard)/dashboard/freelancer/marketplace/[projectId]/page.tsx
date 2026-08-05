import {
    MarketplaceProjectDetailsScreen,
} from
    "@/features/projects/components/marketplace-project-details-screen";

type FreelancerMarketplaceProjectPageProps = {
    params: Promise<{
        projectId: string;
    }>;
};

export default async function FreelancerMarketplaceProjectPage({
    params,
}: FreelancerMarketplaceProjectPageProps) {
    const { projectId } =
        await params;

    return (
        <MarketplaceProjectDetailsScreen
            projectId={projectId}
        />
    );
}