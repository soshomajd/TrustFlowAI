"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { motion } from "motion/react";
import { ArrowLeft, KeyRound, LoaderCircle, LockKeyhole, Mail, } from "lucide-react";
import Link from "next/link";
import { useForm, useWatch } from "react-hook-form";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle, } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { useResendResetCode, useResetPassword, } from "@/features/auth/hooks/use-account-recovery";
import { resendEmailSchema, resetPasswordSchema, type ResetPasswordFormValues, } from "@/features/auth/schemas/recovery-schemas";

type ResetPasswordFormProps = {
    email: string;
};

export function ResetPasswordForm({ email }: ResetPasswordFormProps) {
    const resetMutation = useResetPassword();
    const resendMutation = useResendResetCode();

    const { register, control, handleSubmit, formState: { errors, isSubmitting }, } = useForm<ResetPasswordFormValues>({
        resolver: zodResolver(resetPasswordSchema),
        defaultValues: {
            email,
            code: "",
            newPassword: "",
            confirmPassword: "",
        },
    });

    const enteredEmail = useWatch({ control, name: "email" });

    const isPending = resetMutation.isPending || isSubmitting;
    const emailIsValid = resendEmailSchema.safeParse(enteredEmail).success;

    function onSubmit(values: ResetPasswordFormValues) {
        resetMutation.mutate({
            email: values.email,
            code: values.code,
            newPassword: values.newPassword,
        });
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
                        <CardTitle className="text-2xl font-semibold tracking-tight">
                            Choose a new password
                        </CardTitle>

                        <CardDescription className="mt-2">
                            Enter the 6 digit code we emailed you and pick a new password. The code works for 15 minutes.
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

                        <div className="space-y-2">
                            <Label htmlFor="code">
                                Reset code
                            </Label>

                            <Input
                                id="code"
                                inputMode="numeric"
                                autoComplete="one-time-code"
                                maxLength={6}
                                placeholder="123456"
                                disabled={isPending}
                                aria-invalid={Boolean(errors.code)}
                                className="h-12 text-center text-xl tracking-[0.5em]"
                                {...register("code")}
                            />

                            {errors.code && (
                                <p className="text-sm text-destructive">
                                    {errors.code.message}
                                </p>
                            )}
                        </div>

                        <div className="space-y-2">
                            <Label htmlFor="newPassword">
                                New password
                            </Label>

                            <div className="relative">
                                <LockKeyhole className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />

                                <Input
                                    id="newPassword"
                                    type="password"
                                    autoComplete="new-password"
                                    placeholder="At least 8 characters, with a number"
                                    disabled={isPending}
                                    aria-invalid={Boolean(errors.newPassword)}
                                    className="h-11 pl-10"
                                    {...register("newPassword")}
                                />
                            </div>

                            {errors.newPassword && (
                                <p className="text-sm text-destructive">
                                    {errors.newPassword.message}
                                </p>
                            )}
                        </div>

                        <div className="space-y-2">
                            <Label htmlFor="confirmPassword">
                                Confirm new password
                            </Label>

                            <div className="relative">
                                <LockKeyhole className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />

                                <Input
                                    id="confirmPassword"
                                    type="password"
                                    autoComplete="new-password"
                                    placeholder="Repeat the new password"
                                    disabled={isPending}
                                    aria-invalid={Boolean(errors.confirmPassword)}
                                    className="h-11 pl-10"
                                    {...register("confirmPassword")}
                                />
                            </div>

                            {errors.confirmPassword && (
                                <p className="text-sm text-destructive">
                                    {errors.confirmPassword.message}
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
                                    Changing password
                                </>
                            ) : (
                                "Change password"
                            )}
                        </Button>

                        <div className="flex items-center justify-between gap-3 text-sm">
                            <Button
                                type="button"
                                variant="ghost"
                                size="sm"
                                disabled={resendMutation.isPending || !emailIsValid}
                                onClick={() => {
                                    const parsed = resendEmailSchema.safeParse(enteredEmail);

                                    if (parsed.success) {
                                        resendMutation.mutate(parsed.data);
                                    }
                                }}
                            >
                                {resendMutation.isPending && (
                                    <LoaderCircle className="size-4 animate-spin" />
                                )}
                                Send a new code
                            </Button>

                            <Link
                                href="/login"
                                className="inline-flex items-center gap-1 font-medium text-electric transition hover:text-electric/80"
                            >
                                <ArrowLeft className="size-3.5" />
                                Back to sign in
                            </Link>
                        </div>
                    </form>
                </CardContent>
            </Card>
        </motion.div>
    );
}
