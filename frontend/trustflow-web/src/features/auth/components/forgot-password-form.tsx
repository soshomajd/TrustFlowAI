"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { motion } from "motion/react";
import { ArrowLeft, ArrowRight, KeyRound, LoaderCircle, Mail, } from "lucide-react";
import Link from "next/link";
import { useForm } from "react-hook-form";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle, } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { useForgotPassword } from "@/features/auth/hooks/use-account-recovery";
import { forgotPasswordSchema, type ForgotPasswordFormValues, } from "@/features/auth/schemas/recovery-schemas";

export function ForgotPasswordForm({ admin = false }: { admin?: boolean }) {
    const forgotMutation = useForgotPassword(admin);

    const { register, handleSubmit, formState: { errors, isSubmitting }, } = useForm<ForgotPasswordFormValues>({
        resolver: zodResolver(forgotPasswordSchema),
        defaultValues: {
            email: "",
        },
    });

    const isPending = forgotMutation.isPending || isSubmitting;

    function onSubmit(values: ForgotPasswordFormValues) {
        forgotMutation.mutate(values.email);
    }

    return (
        <motion.div
            initial={{
                opacity: 0,
                y: 18,
            }}
            animate={{
                opacity: 1,
                y: 0,
            }}
            transition={{
                duration: 0.4,
                ease: "easeOut",
            }}
            className="w-full max-w-md"
        >
            <Card className="border-border/80 bg-card/90 shadow-blue-glow backdrop-blur-xl">
                <CardHeader className="space-y-3">
                    <div className="flex size-12 items-center justify-center rounded-xl border border-primary/30 bg-primary/10 shadow-cyan-glow">
                        <KeyRound className="size-6 text-electric" />
                    </div>

                    <div>
                        <CardTitle role="heading" aria-level={1} className="text-2xl font-semibold tracking-tight">
                            {admin ? "Recover administrator password" : "Forgot your password?"}
                        </CardTitle>

                        <CardDescription className="mt-2">
                            Enter your email and we will send you a 6 digit code to choose a new password.
                        </CardDescription>
                    </div>
                </CardHeader>

                <CardContent>
                    <form
                        onSubmit={handleSubmit(onSubmit)}
                        noValidate
                        className="space-y-5"
                    >
                        <div className="space-y-2">
                            <Label htmlFor="email">
                                Email
                            </Label>

                            <div className="relative">
                                <Mail className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />

                                <Input
                                    id="email"
                                    type="email"
                                    autoComplete="email"
                                    placeholder="name@example.com"
                                    disabled={isPending}
                                    aria-invalid={Boolean(errors.email)}
                                    className="h-11 pl-10"
                                    {...register("email")}
                                />
                            </div>

                            {errors.email && (
                                <p className="text-sm text-destructive">
                                    {errors.email.message}
                                </p>
                            )}
                        </div>

                        <Button
                            type="submit"
                            disabled={isPending}
                            className="h-11 w-full shadow-blue-glow transition hover:brightness-110"
                        >
                            {isPending ? (
                                <>
                                    <LoaderCircle className="size-4 animate-spin" />
                                    Sending code
                                </>
                            ) : (
                                <>
                                    Send code
                                    <ArrowRight className="size-4" />
                                </>
                            )}
                        </Button>

                        <p className="text-center text-sm text-muted-foreground">
                            <Link
                                href={admin ? "/admin/login" : "/login"}
                                className="inline-flex items-center gap-1 font-medium text-electric transition hover:text-electric/80"
                            >
                                <ArrowLeft className="size-3.5" />
                                Back to sign in
                            </Link>
                        </p>
                    </form>
                </CardContent>
            </Card>
        </motion.div>
    );
}
