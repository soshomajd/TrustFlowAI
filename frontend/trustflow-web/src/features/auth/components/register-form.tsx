"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { motion } from "motion/react";
import { ArrowRight, BriefcaseBusiness, LoaderCircle, LockKeyhole, Mail, UserRound, } from "lucide-react";
import Link from "next/link";
import { Controller, useForm, } from "react-hook-form";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle, } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue, } from "@/components/ui/select";
import { useRegister } from "@/features/auth/hooks/use-register";
import { registerSchema, type RegisterFormValues, } from "@/features/auth/schemas/register-schema";

export function RegisterForm() {
    const registerMutation = useRegister();

    const { register, control, handleSubmit, formState: { errors, isSubmitting, }, } = useForm<RegisterFormValues>({
        resolver: zodResolver(registerSchema),

        defaultValues: {
            fullName: "",
            email: "",
            password: "",
            confirmPassword: "",
            role: "Client",
        },
    });

    const isPending =
        registerMutation.isPending ||
        isSubmitting;

    function onSubmit(
        values: RegisterFormValues,
    ) {
        registerMutation.mutate({
            fullName: values.fullName,
            email: values.email,
            password: values.password,
            role: values.role,
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
            className="w-full max-w-lg"
        >
            <Card className="border-border/80 bg-card/90 shadow-blue-glow backdrop-blur-xl">
                <CardHeader className="space-y-3">
                    <div className="flex size-12 items-center justify-center rounded-xl border border-primary/30 bg-primary/10 shadow-cyan-glow">
                        <UserRound className="size-6 text-electric" />
                    </div>

                    <div>
                        <CardTitle className="text-2xl font-semibold tracking-tight">
                            Create your account
                        </CardTitle>

                        <CardDescription className="mt-2">
                            Join TrustFlow AI as a
                            client or freelancer.
                        </CardDescription>
                    </div>
                </CardHeader>

                <CardContent>
                    <form
                        onSubmit={handleSubmit(
                            onSubmit,
                        )}
                        noValidate
                        className="space-y-5"
                    >
                        <div className="space-y-2">
                            <Label htmlFor="fullName">
                                Full name
                            </Label>

                            <div className="relative">
                                <UserRound className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />

                                <Input
                                    id="fullName"
                                    autoComplete="name"
                                    placeholder="Your full name"
                                    disabled={isPending}
                                    aria-invalid={Boolean(
                                        errors.fullName,
                                    )}
                                    className="h-11 pl-10"
                                    {...register(
                                        "fullName",
                                    )}
                                />
                            </div>

                            {errors.fullName && (
                                <p className="text-sm text-destructive">
                                    {
                                        errors.fullName
                                            .message
                                    }
                                </p>
                            )}
                        </div>

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
                                    aria-invalid={Boolean(
                                        errors.email,
                                    )}
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
                            <Label>
                                Account type
                            </Label>

                            <Controller
                                name="role"
                                control={control}
                                render={({ field }) => (
                                    <Select
                                        value={field.value}
                                        onValueChange={
                                            field.onChange
                                        }
                                        disabled={isPending}
                                    >
                                        <SelectTrigger className="h-11 w-full">
                                            <div className="flex items-center gap-2">
                                                <BriefcaseBusiness className="size-4 text-muted-foreground" />

                                                <SelectValue />
                                            </div>
                                        </SelectTrigger>

                                        <SelectContent>
                                            <SelectItem value="Client">
                                                Client
                                            </SelectItem>

                                            <SelectItem value="Freelancer">
                                                Freelancer
                                            </SelectItem>
                                        </SelectContent>
                                    </Select>
                                )}
                            />

                            {errors.role && (
                                <p className="text-sm text-destructive">
                                    {errors.role.message}
                                </p>
                            )}
                        </div>

                        <div className="grid gap-5 sm:grid-cols-2">
                            <div className="space-y-2">
                                <Label htmlFor="password">
                                    Password
                                </Label>

                                <div className="relative">
                                    <LockKeyhole className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />

                                    <Input
                                        id="password"
                                        type="password"
                                        autoComplete="new-password"
                                        placeholder="Minimum 8 characters"
                                        disabled={isPending}
                                        aria-invalid={Boolean(
                                            errors.password,
                                        )}
                                        className="h-11 pl-10"
                                        {...register(
                                            "password",
                                        )}
                                    />
                                </div>

                                {errors.password && (
                                    <p className="text-sm text-destructive">
                                        {
                                            errors.password
                                                .message
                                        }
                                    </p>
                                )}
                            </div>

                            <div className="space-y-2">
                                <Label htmlFor="confirmPassword">
                                    Confirm password
                                </Label>

                                <div className="relative">
                                    <LockKeyhole className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />

                                    <Input
                                        id="confirmPassword"
                                        type="password"
                                        autoComplete="new-password"
                                        placeholder="Repeat password"
                                        disabled={isPending}
                                        aria-invalid={Boolean(
                                            errors.confirmPassword,
                                        )}
                                        className="h-11 pl-10"
                                        {...register(
                                            "confirmPassword",
                                        )}
                                    />
                                </div>

                                {errors.confirmPassword && (
                                    <p className="text-sm text-destructive">
                                        {
                                            errors
                                                .confirmPassword
                                                .message
                                        }
                                    </p>
                                )}
                            </div>
                        </div>

                        <Button
                            type="submit"
                            disabled={isPending}
                            className="h-11 w-full shadow-blue-glow transition hover:brightness-110"
                        >
                            {isPending ? (
                                <>
                                    <LoaderCircle className="size-4 animate-spin" />
                                    Creating account
                                </>
                            ) : (
                                <>
                                    Create account
                                    <ArrowRight className="size-4" />
                                </>
                            )}
                        </Button>

                        <p className="text-center text-sm text-muted-foreground">
                            Already have an account?{" "}
                            <Link
                                href="/login"
                                className="font-medium text-electric transition hover:text-electric/80"
                            >
                                Sign in
                            </Link>
                        </p>
                    </form>
                </CardContent>
            </Card>
        </motion.div>
    );
}