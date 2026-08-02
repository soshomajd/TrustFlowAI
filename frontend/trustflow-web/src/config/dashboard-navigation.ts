import type { LucideIcon } from "lucide-react";
import {
  BriefcaseBusiness,
  FilePlus2,
  FolderKanban,
  LayoutDashboard,
  Search,
  Send,
} from "lucide-react";

import type { AuthRole } from "@/features/auth/types/auth";

export type DashboardNavigationItem = {
  label: string;
  href: string;
  icon: LucideIcon;
  roles: AuthRole[];
};

export const dashboardNavigationItems: DashboardNavigationItem[] = [
  {
    label: "Overview",
    href: "/dashboard/client",
    icon: LayoutDashboard,
    roles: ["Client"],
  },
  {
    label: "My projects",
    href: "/dashboard/client/projects",
    icon: FolderKanban,
    roles: ["Client"],
  },
  {
    label: "Create project",
    href: "/dashboard/client/projects/new",
    icon: FilePlus2,
    roles: ["Client"],
  },
  {
    label: "Overview",
    href: "/dashboard/freelancer",
    icon: LayoutDashboard,
    roles: ["Freelancer"],
  },
  {
    label: "Marketplace",
    href: "/dashboard/freelancer/marketplace",
    icon: Search,
    roles: ["Freelancer"],
  },
  {
    label: "My proposals",
    href: "/dashboard/freelancer/proposals",
    icon: Send,
    roles: ["Freelancer"],
  },
  {
    label: "Assigned projects",
    href: "/dashboard/freelancer/projects",
    icon: BriefcaseBusiness,
    roles: ["Freelancer"],
  },
];

export function getDashboardNavigation(roles: AuthRole[]) {
  return dashboardNavigationItems.filter((item) =>
    item.roles.some((role) => roles.includes(role)),
  );
}
export function getActiveDashboardNavigationItem(
  pathname: string,
  roles: AuthRole[],
) {
  const navigation = getDashboardNavigation(roles);

  return navigation
    .filter((item) => {
      return pathname === item.href || pathname.startsWith(`${item.href}/`);
    })
    .sort(
      (firstItem, secondItem) => secondItem.href.length - firstItem.href.length,
    )[0];
}
