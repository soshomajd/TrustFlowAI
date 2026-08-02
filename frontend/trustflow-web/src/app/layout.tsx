import type { Metadata } from "next";
import { Oxanium } from "next/font/google";
import { Toaster } from
  "@/components/ui/sonner";
import { AppProviders } from
  "@/providers/app-providers";
import "./globals.css";

const oxanium = Oxanium({
  subsets: ["latin"],
  weight: ["400", "500", "600", "700"],
  variable: "--font-oxanium",
  display: "swap",
});

export const metadata: Metadata = {
  title: {
    default: "TrustFlow AI",
    template: "%s | TrustFlow AI",
  },
  description:
    "A secure milestone-based freelance marketplace.",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html
      data-scroll-behavior="smooth"
      lang="en"
      className={`dark ${oxanium.variable}`}
      suppressHydrationWarning
    >
      <body className="min-h-screen font-sans antialiased">
        <AppProviders>
          {children}

          <Toaster
            position="top-right"
            richColors
            closeButton
          />
        </AppProviders>
      </body>
    </html>
  );
}