"use client";
import { Avatar, AvatarImage, AvatarFallback } from "@/components/ui/avatar";
import { useProfilePhoto } from "@/features/profile-photos/hooks/use-profile-photo";
import { cn } from "@/lib/utils";
export function UserAvatar({ userId, name, className }: { userId?: string | null; name?: string | null; className?: string }) {
  const photo = useProfilePhoto(userId);
  const initials = name?.trim().split(/\s+/).slice(0, 2).map((part) => part[0]).join("").toUpperCase() || "?";
  return <Avatar className={cn("size-11 inline-flex align-middle border border-primary/25 bg-primary/10", className)}>
    {photo.data?.image && <AvatarImage src={photo.data.image} alt={name ? name + "'s profile photo" : "Profile photo"} />}
    <AvatarFallback className="bg-primary/10 font-semibold text-electric">{initials}</AvatarFallback>
  </Avatar>;
}
export function MemberName({ userId, name }: { userId?: string | null; name?: string | null }) {
  return <span className="inline-flex max-w-full items-center gap-2 align-middle"><UserAvatar userId={userId} name={name} /><span className="min-w-0 break-words">{name || "Unknown member"}</span></span>;
}
