"use client";

import {
  Bell,
  CircleAlert,
  CircleCheck,
  Info,
  LoaderCircle,
  TriangleAlert,
} from "lucide-react";
import Link from "next/link";
import { useState } from "react";

import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { useNotifications } from "@/features/notifications/hooks/use-notifications";
import type {
  NotificationItem,
  NotificationSeverity,
} from "@/features/notifications/types/notification";
import { cn } from "@/lib/utils";

const severityStyles: Record<
  NotificationSeverity,
  { icon: typeof Info; dot: string; text: string }
> = {
  action: {
    icon: CircleAlert,
    dot: "bg-electric",
    text: "text-electric",
  },
  warning: {
    icon: TriangleAlert,
    dot: "bg-amber-400",
    text: "text-amber-400",
  },
  info: {
    icon: Info,
    dot: "bg-muted-foreground",
    text: "text-muted-foreground",
  },
};

export function NotificationBell() {
  const [isOpen, setIsOpen] = useState(false);

  const notificationsQuery = useNotifications();

  const items = notificationsQuery.data?.items ?? [];
  const actionCount = notificationsQuery.data?.actionCount ?? 0;

  return (
    <DropdownMenu open={isOpen} onOpenChange={setIsOpen}>
      <DropdownMenuTrigger asChild>
        <Button
          type="button"
          variant="ghost"
          size="icon"
          className="relative"
          aria-label={
            actionCount > 0
              ? `Notifications, ${actionCount} waiting for you`
              : "Notifications"
          }
        >
          <Bell className="size-5" />

          {actionCount > 0 && (
            <span
              className="absolute -top-0.5 -right-0.5 flex min-w-4.5 items-center justify-center rounded-full bg-destructive px-1 text-[11px] font-semibold text-white"
              aria-hidden
            >
              {actionCount > 9 ? "9+" : actionCount}
            </span>
          )}
        </Button>
      </DropdownMenuTrigger>

      <DropdownMenuContent
        align="end"
        className="w-[min(22rem,calc(100vw-2rem))] p-0"
      >
        <div className="flex items-center justify-between border-b px-3 py-2.5">
          <p className="text-sm font-semibold">What needs you</p>

          {notificationsQuery.isFetching && (
            <LoaderCircle className="size-3.5 animate-spin text-muted-foreground" />
          )}
        </div>

        <div className="max-h-[22rem] overflow-y-auto">
          {notificationsQuery.isLoading ? (
            <p className="px-3 py-6 text-center text-sm text-muted-foreground">
              Loading
            </p>
          ) : notificationsQuery.isError ? (
            <div className="px-3 py-6 text-center">
              <p className="text-sm text-destructive">
                Notifications could not be loaded.
              </p>

              <Button
                type="button"
                size="sm"
                variant="outline"
                className="mt-3"
                onClick={() => void notificationsQuery.refetch()}
              >
                Retry
              </Button>
            </div>
          ) : items.length === 0 ? (
            <div className="px-3 py-8 text-center">
              <CircleCheck className="mx-auto size-6 text-emerald-400" />

              <p className="mt-2 text-sm font-medium">You are all caught up</p>

              <p className="mt-1 text-xs text-muted-foreground">
                Nothing is waiting for you right now.
              </p>
            </div>
          ) : (
            <ul className="divide-y">
              {items.map((item) => (
                <NotificationRow
                  key={item.id}
                  item={item}
                  onNavigate={() => {
                    setIsOpen(false);
                  }}
                />
              ))}
            </ul>
          )}
        </div>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

function NotificationRow({
  item,
  onNavigate,
}: {
  item: NotificationItem;
  onNavigate: () => void;
}) {
  const style = severityStyles[item.severity] ?? severityStyles.info;
  const Icon = style.icon;

  return (
    <li>
      <Link
        href={item.href}
        onClick={onNavigate}
        className="flex gap-2.5 px-3 py-3 transition hover:bg-muted/60"
      >
        <Icon className={cn("mt-0.5 size-4 shrink-0", style.text)} />

        <div className="min-w-0">
          <p className="text-sm font-medium break-words">{item.title}</p>

          <p className="mt-0.5 text-xs break-words text-muted-foreground">
            {item.message}
          </p>

          <p className="mt-1 flex items-center gap-1.5 text-xs text-muted-foreground">
            <span className={cn("size-1.5 rounded-full", style.dot)} />
            <span className="truncate">{item.projectTitle}</span>
          </p>
        </div>
      </Link>
    </li>
  );
}
