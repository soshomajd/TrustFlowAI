"use client";
import { useEffect, useRef, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Camera, LoaderCircle, Trash2 } from "lucide-react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Avatar, AvatarImage } from "@/components/ui/avatar";
import { UserAvatar } from "@/features/profile-photos/components/user-avatar";
import { photoKey, useProfilePhoto, type ProfilePhoto } from "@/features/profile-photos/hooks/use-profile-photo";
import { apiClient } from "@/lib/api/api-client";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";
import { useAuthStore } from "@/stores/auth-store";

type Selection = { file: File; url: string; width: number; height: number };
export function ProfilePhotoEditor({ userId, name }: { userId: string; name: string }) {
  const queryClient = useQueryClient();
  const viewer = useAuthStore((state) => state.user?.id ?? "");
  const current = useProfilePhoto(userId);
  const input = useRef<HTMLInputElement>(null);
  const [selection, setSelection] = useState<Selection | null>(null);
  const [zoom, setZoom] = useState(1);
  const [x, setX] = useState(50);
  const [y, setY] = useState(50);
  const [error, setError] = useState("");
  const [reading, setReading] = useState(false);
  const active = useRef(0);
  useEffect(() => () => { active.current++; }, []);
  useEffect(() => () => { if (selection) URL.revokeObjectURL(selection.url); }, [selection]);
  const save = useMutation({ mutationFn: async () => {
    if (!selection) throw new Error("Choose a photo first.");
    const form = new FormData(); form.append("file", selection.file);
    form.append("zoom", String(zoom)); form.append("x", String(x)); form.append("y", String(y));
    return (await apiClient.post<ProfilePhoto>("/users/me/photo", form)).data;
  }, onSuccess: (photo) => { queryClient.setQueryData(photoKey(viewer, userId), photo); setSelection(null); toast.success("Profile photo saved."); } });
  const remove = useMutation({ mutationFn: () => apiClient.delete("/users/me/photo"), onSuccess: () => {
    queryClient.setQueryData(photoKey(viewer, userId), { image: null }); setSelection(null); toast.success("Profile photo removed.");
  } });
  const busy = reading || save.isPending || remove.isPending;
  async function choose(file?: File) {
    if (!file) return;
    setError(""); save.reset(); remove.reset();
    if (!["image/jpeg", "image/png", "image/webp"].includes(file.type) || !/\.(jpe?g|png|webp)$/i.test(file.name) || file.size > 5 * 1024 * 1024 || !file.size) {
      setError("Choose a JPG, PNG or WebP photo up to 5 MB."); return;
    }
    const attempt = ++active.current;
    setReading(true);
    const url = URL.createObjectURL(file);
    const image = new window.Image(); image.src = url;
    try {
      await image.decode();
      if (attempt !== active.current) { URL.revokeObjectURL(url); return; }
      if (image.naturalWidth < 128 || image.naturalHeight < 128 || image.naturalWidth > 8000 || image.naturalHeight > 8000 || image.naturalWidth * image.naturalHeight > 24_000_000) throw new Error("Choose a photo at least 128 pixels wide and tall, up to 24 megapixels.");
      setSelection({ file, url, width: image.naturalWidth, height: image.naturalHeight }); setZoom(1); setX(50); setY(50);
    } catch (err) { URL.revokeObjectURL(url); if (attempt === active.current) setError(err instanceof Error ? err.message : "This photo could not be opened."); }
    finally { if (attempt === active.current) setReading(false); }
  }
  const scale = selection ? 224 / Math.min(selection.width, selection.height) * zoom : 1;
  const width = (selection?.width ?? 224) * scale, height = (selection?.height ?? 224) * scale;
  const message = error || (save.isError ? getApiErrorMessage(save.error) : remove.isError ? getApiErrorMessage(remove.error) : "");
  return <section className="mb-6 rounded-2xl border bg-card/80 p-5 sm:p-7" aria-labelledby="profile-photo-title">
    <div className="flex flex-wrap items-center gap-5">
      <UserAvatar userId={userId} name={name} className="size-24" />
      <div className="min-w-0 flex-1"><h2 id="profile-photo-title" className="text-lg font-semibold">Profile photo</h2>
        <p className="mt-1 text-sm text-muted-foreground">Choose a clear photo of your face. It appears beside your name.</p>
        <p className="mt-1 text-xs text-muted-foreground">JPG, PNG or WebP. Up to 5 MB.</p>
        <div className="mt-4 flex flex-wrap gap-2">
          <input ref={input} type="file" accept="image/jpeg,image/png,image/webp" className="sr-only" tabIndex={-1} aria-label="Choose profile photo" disabled={busy} onChange={(event) => { void choose(event.target.files?.[0]); event.target.value = ""; }} />
          <Button type="button" variant="outline" disabled={busy} onClick={() => input.current?.click()}><Camera className="size-4" />{current.data?.image ? "Change photo" : "Add photo"}</Button>
          {current.data?.image && <Button type="button" variant="ghost" disabled={busy} onClick={() => { setError(""); save.reset(); remove.mutate(); }}><Trash2 className="size-4" />Remove photo</Button>}
        </div>
      </div>
    </div>
    {reading && <p className="mt-4 text-sm" role="status">Opening photo...</p>}
    {selection && <div className="mt-6 flex flex-col items-center gap-6 border-t pt-6 sm:flex-row">
      <div className="relative size-56 shrink-0 overflow-hidden rounded-full border-2 border-primary/50 bg-background" aria-label="Round photo preview">
        <Avatar className="absolute rounded-none" style={{ width, height, left: (224 - width) * x / 100, top: (224 - height) * y / 100 }}>
          <AvatarImage src={selection.url} alt="Preview of your profile photo" className="rounded-none" />
        </Avatar>
      </div>
      <div className="w-full max-w-sm space-y-4"><p className="text-sm font-medium">Keep your face inside the circle.</p>
        <label className="block text-sm">Zoom<input className="mt-2 block w-full accent-blue-500" type="range" min="1" max="3" step="0.05" value={zoom} disabled={busy} onChange={(e) => setZoom(Number(e.target.value))} /></label>
        <label className="block text-sm">Move left or right<input className="mt-2 block w-full accent-blue-500" type="range" min="0" max="100" value={x} disabled={busy} onChange={(e) => setX(Number(e.target.value))} /></label>
        <label className="block text-sm">Move up or down<input className="mt-2 block w-full accent-blue-500" type="range" min="0" max="100" value={y} disabled={busy} onChange={(e) => setY(Number(e.target.value))} /></label>
        <div className="flex gap-2"><Button type="button" disabled={busy} onClick={() => save.mutate()}>{save.isPending && <LoaderCircle className="size-4 animate-spin" />}Save photo</Button><Button type="button" variant="ghost" disabled={busy} onClick={() => { setSelection(null); setError(""); save.reset(); }}>Cancel</Button></div>
      </div>
    </div>}
    {message && <p role="alert" className="mt-4 text-sm text-destructive">{message}</p>}
  </section>;
}
