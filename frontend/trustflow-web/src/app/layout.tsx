import type { Metadata, Viewport } from "next";
import { Oxanium } from "next/font/google";

import { site } from "@/config/site";
import "./globals.css";

const oxanium = Oxanium({
  subsets: ["latin"],
  weight: ["400", "500", "600", "700"],
  variable: "--font-oxanium",
  display: "swap",
});

export const metadata: Metadata = {
  metadataBase: new URL(site.url),
  applicationName: site.name,
  title: {
    default: `${site.name} | ${site.title}`,
    template: `%s | ${site.name}`,
  },
  description: site.description,
  icons: {
    icon: [
      {
        url: "/trustflow-ai-favicon-32.png",
        sizes: "32x32",
        type: "image/png",
      },
      { url: "/favicon.ico", sizes: "any" },
    ],
  },
  openGraph: {
    type: "website",
    siteName: site.name,
    locale: site.locale,
    title: `${site.name} | ${site.title}`,
    description: site.description,
  },
  twitter: {
    card: "summary_large_image",
    title: `${site.name} | ${site.title}`,
    description: site.description,
  },
};

export const viewport: Viewport = {
  themeColor: "#060a11",
  colorScheme: "dark",
};

// The query client, wallet, toasts and session check belong to the screens that
// use them (see the auth and dashboard layouts), so the public pages ship much
// less script.
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
        {children}
      </body>
    </html>
  );
}
