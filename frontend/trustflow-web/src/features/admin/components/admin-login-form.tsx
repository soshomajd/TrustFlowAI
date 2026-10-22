"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import Link from "next/link";
import { ShieldCheck, LoaderCircle } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { AdminMfaForm } from "@/features/admin/components/admin-mfa-form";
import { useAdminLogin } from "@/features/admin/hooks/use-admin-auth";
import { loginSchema, type LoginFormValues } from "@/features/auth/schemas/login-schema";
import { getAdminReturnPath } from "@/features/auth/lib/sign-in-path";
import { useAuthStore } from "@/stores/auth-store";

export function AdminLoginForm({ next }: { next?: string }) {
  const mutation = useAdminLogin();
  const router = useRouter();
  const user = useAuthStore((state) => state.user);
  const status = useAuthStore((state) => state.status);
  useEffect(() => {
    if (status === "authenticated" && user?.roles.includes("Admin")) router.replace(getAdminReturnPath(next));
  }, [next, router, status, user]);
  const { register, handleSubmit, reset, formState: { errors, isSubmitting } } = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema), defaultValues: { email: "", password: "" },
  });
  const pending = mutation.isPending || isSubmitting;
  if (mutation.data?.mfaRequired) return <AdminMfaForm prompt={mutation.data} next={next} onRestart={() => { mutation.reset(); reset(); }} />;
  return <section className="w-full max-w-md rounded-2xl border border-primary/30 bg-card/95 p-7 shadow-blue-glow">
    <ShieldCheck className="size-10 text-electric" />
    <p className="mt-5 text-xs font-semibold uppercase tracking-[0.2em] text-electric">TrustFlow administration</p>
    <h1 className="mt-2 text-2xl font-semibold">Administrator sign in</h1>
    <p className="mt-3 text-sm text-muted-foreground">Enter your administrator password. A one-time sign-in code will then be sent to your approved email address.</p>
    {status === "authenticated" && !user?.roles.includes("Admin") && <p role="status" className="mt-4 rounded-lg border p-3 text-sm">Your current account does not have administrator access. Use your administrator credentials below.</p>}
    <form onSubmit={handleSubmit((values) => mutation.mutate(values))} className="mt-6 space-y-5" noValidate>
      <div className="space-y-2"><Label htmlFor="admin-email">Administrator email</Label><Input id="admin-email" type="email" autoComplete="username" disabled={pending} aria-invalid={Boolean(errors.email)} {...register("email")} />{errors.email && <p role="alert" className="text-sm text-destructive">{errors.email.message}</p>}</div>
      <div className="space-y-2"><Label htmlFor="admin-password">Password</Label><Input id="admin-password" type="password" autoComplete="current-password" disabled={pending} aria-invalid={Boolean(errors.password)} {...register("password")} />{errors.password && <p role="alert" className="text-sm text-destructive">{errors.password.message}</p>}</div>
      <Button type="submit" className="w-full" disabled={pending}>{pending && <LoaderCircle className="size-4 animate-spin" />} {pending ? "Signing in…" : "Send administrator sign-in code"}</Button>
    </form>
    <div className="mt-5 space-y-3 text-sm"><Link className="block text-electric hover:underline" href="/admin/forgot-password">Forgot administrator password?</Link><Link className="block text-electric hover:underline" href="/admin/setup">Activate an approved administrator account</Link></div>
    <p className="mt-5 text-xs text-muted-foreground">Administrator access is limited to accounts approved by the platform owner.</p>
  </section>;
}
