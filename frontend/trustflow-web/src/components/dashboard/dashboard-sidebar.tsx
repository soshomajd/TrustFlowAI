"use client";

import { ShieldCheck } from "lucide-react";
import Link from "next/link";
import { usePathname } from "next/navigation";

import { cn } from "@/lib/utils";
import { getDashboardNavigation, getActiveDashboardNavigationItem } from "@/config/dashboard-navigation";
import { LogoutButton } from "@/features/auth/components/logout-button";
import { useAuthStore } from "@/stores/auth-store";
import { useUiStore } from "@/stores/ui-store";


type DashboardSidebarProps = {
    mobile?: boolean;
};

export function DashboardSidebar({
    mobile = false,
}: DashboardSidebarProps) {
    const pathname = usePathname();

    const user = useAuthStore(
        (state) => state.user,
    );

    const isSidebarCollapsed =
        useUiStore(
            (state) =>
                state.isSidebarCollapsed,
        );

    const setMobileSidebarOpen =
        useUiStore(
            (state) =>
                state.setMobileSidebarOpen,
        );

    const collapsed =
        isSidebarCollapsed && !mobile;

    const navigation =
        getDashboardNavigation(
            user?.roles ?? [],
        );
    const activeNavigationItem =
        getActiveDashboardNavigationItem(
            pathname,
            user?.roles ?? [],
        );


    function closeMobileSidebar() {
        if (mobile) {
            setMobileSidebarOpen(false);
        }
    }

    return (
        <div className="flex h-full flex-col bg-sidebar">
            <div
                className={cn(
                    "flex h-16 items-center border-b border-sidebar-border px-4",
                    collapsed
                        ? "justify-center"
                        : "gap-3",
                )}
            >
                <Link
                    href="/dashboard"
                    onClick={closeMobileSidebar}
                    className="flex items-center gap-3"
                >
                    <div className="flex size-10 shrink-0 items-center justify-center rounded-xl border border-primary/30 bg-primary/10 shadow-cyan-glow">
                        <ShieldCheck className="size-5 text-electric" />
                    </div>

                    {!collapsed && (
                        <div>
                            <p className="font-semibold leading-none text-sidebar-foreground">
                                TrustFlow AI
                            </p>

                            <p className="mt-1 text-[10px] uppercase tracking-[0.2em] text-muted-foreground">
                                Secure workspace
                            </p>
                        </div>
                    )}
                </Link>
            </div>

            <nav className="flex-1 space-y-1 overflow-y-auto p-3">
                {!collapsed && (
                    <p className="mb-3 px-3 text-[10px] font-semibold uppercase tracking-[0.2em] text-muted-foreground">
                        Workspace
                    </p>
                )}

                {navigation.map((item) => {
                    const Icon = item.icon;

                    const isActive = activeNavigationItem?.href === item.href;
                    return (
                        <Link
                            key={item.href}
                            href={item.href}
                            title={
                                collapsed
                                    ? item.label
                                    : undefined
                            }
                            onClick={
                                closeMobileSidebar
                            }
                            className={cn(
                                "flex h-11 items-center rounded-xl text-sm font-medium transition",
                                collapsed
                                    ? "justify-center px-0"
                                    : "gap-3 px-3",

                                isActive
                                    ? "border border-primary/30 bg-primary/15 text-primary-foreground shadow-blue-glow"
                                    : "text-muted-foreground hover:bg-sidebar-accent hover:text-sidebar-accent-foreground",
                            )}
                        >
                            <Icon
                                className={cn(
                                    "size-5 shrink-0",
                                    isActive &&
                                    "text-electric",
                                )}
                            />

                            {!collapsed && (
                                <span>{item.label}</span>
                            )}
                        </Link>
                    );
                })}
            </nav>

            <div className="border-t border-sidebar-border p-3">
                {!collapsed && (
                    <div className="mb-3 rounded-xl border border-sidebar-border bg-sidebar-accent/50 p-3">
                        <p className="truncate text-sm font-medium">
                            {user?.fullName}
                        </p>

                        <p className="mt-1 truncate text-xs text-muted-foreground">
                            {user?.email}
                        </p>

                        <p className="mt-2 text-[10px] uppercase tracking-[0.18em] text-electric">
                            {user?.roles[0] ??
                                "User"}
                        </p>
                    </div>
                )}

                <LogoutButton
                    collapsed={collapsed}
                />
            </div>
        </div>
    );
}