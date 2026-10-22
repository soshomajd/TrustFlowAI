import type { Metadata } from "next";
import { ForgotPasswordForm } from "@/features/auth/components/forgot-password-form";

export const metadata: Metadata = { title: "Recover administrator password", robots: { index: false, follow: false } };
export default function AdminForgotPasswordPage() { return <ForgotPasswordForm admin />; }
