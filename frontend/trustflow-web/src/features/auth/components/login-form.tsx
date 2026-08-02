"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { motion } from "motion/react";
import { ArrowRight, LoaderCircle, LockKeyhole, Mail, } from "lucide-react";
import Link from "next/link";
import { useForm } from "react-hook-form";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle, } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { useLogin } from "@/features/auth/hooks/use-login";
import { loginSchema, type LoginFormValues, } from "@/features/auth/schemas/login-schema";

export function LoginForm() {
    const loginMutation = useLogin();

    const { register, handleSubmit, formState: { errors, isSubmitting }, } = useForm<LoginFormValues>
        ({
            resolver: zodResolver(loginSchema),
            defaultValues: {
                email: "",
                password: "",
            },
        });

    const isPending = loginMutation.isPending || isSubmitting;

    function onSubmit(
        values: LoginFormValues,
    ) {
        loginMutation.mutate(values);
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
                        <LockKeyhole className="size-6 text-electric" />
                    </div>

                    <div>
                        <CardTitle className="text-2xl font-semibold tracking-tight">
                            Welcome back
                        </CardTitle>

                        <CardDescription className="mt-2">
                            Sign in to manage your projects,
                            proposals and milestones.
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
                                    aria-invalid={
                                        Boolean(errors.email)
                                    }
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
                            <Label htmlFor="password">
                                Password
                            </Label>

                            <div className="relative">
                                <LockKeyhole className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />

                                <Input
                                    id="password"
                                    type="password"
                                    autoComplete="current-password"
                                    placeholder="Enter your password"
                                    disabled={isPending}
                                    aria-invalid={
                                        Boolean(errors.password)
                                    }
                                    className="h-11 pl-10"
                                    {...register("password")}
                                />
                            </div>

                            {errors.password && (
                                <p className="text-sm text-destructive">
                                    {errors.password.message}
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
                                    Signing in
                                </>
                            ) : (
                                <>
                                    Sign in
                                    <ArrowRight className="size-4" />
                                </>
                            )}
                        </Button>

                        <p className="text-center text-sm text-muted-foreground">
                            Do not have an account?{" "}
                            <Link
                                href="/register"
                                className="font-medium text-electric transition hover:text-electric/80"
                            >
                                Create account
                            </Link>
                        </p>
                    </form>
                </CardContent>
            </Card>
        </motion.div>
    );
}