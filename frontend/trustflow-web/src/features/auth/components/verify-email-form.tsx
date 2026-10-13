"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { motion } from "motion/react";
import { ArrowRight, LoaderCircle, Mail, MailCheck } from "lucide-react";
import Link from "next/link";
import { useEffect, useState } from "react";
import { useForm, useWatch } from "react-hook-form";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle, } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { getResendWaitSeconds, useResendVerification, useVerifyEmail, } from "@/features/auth/hooks/use-account-recovery";
import { resendEmailSchema, verifyEmailSchema, type VerifyEmailFormValues, } from "@/features/auth/schemas/recovery-schemas";

// The server will not send a second code inside this window.
const RESEND_COOLDOWN_SECONDS = 60;

type VerifyEmailFormProps = {
    email: string;
    // True when registration already mailed a code a moment ago.
    codeJustSent: boolean;
};

export function VerifyEmailForm({ email, codeJustSent, }: VerifyEmailFormProps) {
    const verifyMutation = useVerifyEmail();
    const resendMutation = useResendVerification();

    const { register, control, handleSubmit, formState: { errors, isSubmitting }, } = useForm<VerifyEmailFormValues>({
        resolver: zodResolver(verifyEmailSchema),
        defaultValues: {
            email,
            code: "",
        },
    });

    const enteredEmail = useWatch({ control, name: "email" });

    // When the next code may be requested, and a clock that ticks only
    // while a wait is running.
    const [resendAvailableAt, setResendAvailableAt] = useState<number | null>(
        () => (codeJustSent ? Date.now() + RESEND_COOLDOWN_SECONDS * 1000 : null),
    );
    const [now, setNow] = useState(() => Date.now());

    useEffect(() => {
        if (resendAvailableAt === null) {
            return;
        }

        const timer = setInterval(() => setNow(Date.now()), 1000);

        return () => clearInterval(timer);
    }, [resendAvailableAt]);

    const secondsLeft =
        resendAvailableAt === null
            ? 0
            : Math.max(0, Math.ceil((resendAvailableAt - now) / 1000));

    const isPending = verifyMutation.isPending || isSubmitting;
    const emailIsValid = resendEmailSchema.safeParse(enteredEmail).success;

    function startWait(seconds: number) {
        const current = Date.now();

        setNow(current);
        setResendAvailableAt(current + seconds * 1000);
    }

    function onResend() {
        const parsed = resendEmailSchema.safeParse(enteredEmail);

        if (!parsed.success) {
            return;
        }

        resendMutation.mutate(parsed.data, {
            onSuccess: () => startWait(RESEND_COOLDOWN_SECONDS),
            onError: (error) => {
                const wait = getResendWaitSeconds(error);

                if (wait !== null) {
                    startWait(wait);
                }
            },
        });
    }

    function onSubmit(values: VerifyEmailFormValues) {
        verifyMutation.mutate(values);
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
                        <MailCheck className="size-6 text-electric" />
                    </div>

                    <div>
                        <CardTitle role="heading" aria-level={1} className="text-2xl font-semibold tracking-tight">
                            Verify your email
                        </CardTitle>

                        <CardDescription className="mt-2">
                            {codeJustSent
                                ? "We sent a 6 digit code to your email. Enter it to finish creating your account."
                                : "Enter the 6 digit code from your email. If you do not have one, ask for a new code below."}
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
                                Verification code
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

                        <Button
                            type="submit"
                            disabled={isPending}
                            className="h-11 w-full shadow-blue-glow transition hover:brightness-110"
                        >
                            {isPending ? (
                                <>
                                    <LoaderCircle className="size-4 animate-spin" />
                                    Checking
                                </>
                            ) : (
                                <>
                                    Verify email
                                    <ArrowRight className="size-4" />
                                </>
                            )}
                        </Button>

                        <div className="flex items-center justify-between gap-3 text-sm">
                            <Button
                                type="button"
                                variant="ghost"
                                size="sm"
                                disabled={
                                    resendMutation.isPending ||
                                    secondsLeft > 0 ||
                                    !emailIsValid
                                }
                                onClick={onResend}
                            >
                                {resendMutation.isPending && (
                                    <LoaderCircle className="size-4 animate-spin" />
                                )}
                                {secondsLeft > 0
                                    ? `Send a new code in ${secondsLeft}s`
                                    : "Send a new code"}
                            </Button>

                            <Link
                                href="/login"
                                className="font-medium text-electric transition hover:text-electric/80"
                            >
                                Back to sign in
                            </Link>
                        </div>
                    </form>
                </CardContent>
            </Card>
        </motion.div>
    );
}
