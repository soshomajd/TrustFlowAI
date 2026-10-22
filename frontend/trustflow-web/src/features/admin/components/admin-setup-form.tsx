"use client";

import Link from "next/link";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { ShieldCheck, LoaderCircle } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { adminSetupSchema, type AdminSetupValues } from "@/features/admin/schemas/admin-setup-schema";
import { useAdminSetup } from "@/features/admin/hooks/use-admin-auth";

export function AdminSetupForm() {
  const mutation = useAdminSetup();
  const { register, handleSubmit, formState: { errors, isSubmitting } } = useForm<AdminSetupValues>({
    resolver: zodResolver(adminSetupSchema), defaultValues: { fullName: "", email: "", password: "", confirmPassword: "" },
  });
  const pending = mutation.isPending || isSubmitting;
  const fields = [
    ["fullName", "Administrator name", "text", "name"], ["email", "Approved administrator email", "email", "username"],
    ["password", "Choose a password", "password", "new-password"], ["confirmPassword", "Confirm password", "password", "new-password"],
  ] as const;
  return <section className="w-full max-w-md rounded-2xl border border-primary/30 bg-card/95 p-7 shadow-blue-glow">
    <ShieldCheck className="size-10 text-electric" /><h1 className="mt-5 text-2xl font-semibold">Activate administrator access</h1>
    <p className="mt-3 text-sm text-muted-foreground">Only an email approved by the platform owner can activate an administrator account. Email verification is required before sign-in.</p>
    <form className="mt-6 space-y-4" noValidate onSubmit={handleSubmit(({ fullName, email, password }) => mutation.mutate({ fullName, email, password }))}>
      {fields.map(([name, label, type, autoComplete]) => <div key={name} className="space-y-2"><Label htmlFor={`admin-${name}`}>{label}</Label><Input id={`admin-${name}`} type={type} autoComplete={autoComplete} disabled={pending} aria-invalid={Boolean(errors[name])} {...register(name)} />{errors[name] && <p role="alert" className="text-sm text-destructive">{errors[name].message}</p>}</div>)}
      <p className="text-xs text-muted-foreground">Use at least 8 characters, including a lowercase letter and a number.</p>
      <Button type="submit" disabled={pending} className="w-full">{pending && <LoaderCircle className="size-4 animate-spin" />} {pending ? "Activating…" : "Activate approved account"}</Button>
    </form>
    <Link href="/admin/login" className="mt-5 block text-sm text-electric hover:underline">Back to administrator sign in</Link>
  </section>;
}
