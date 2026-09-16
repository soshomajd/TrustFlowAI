import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  reactCompiler: true,
  async redirects() {
    // Keep local pages and the API on the same site for refresh cookies and CORS.
    if (process.env.NODE_ENV !== "development") return [];

    return [{
      source: "/:path*",
      has: [{ type: "host", value: "127\\.0\\.0\\.1" }],
      destination: "http://localhost:3000/:path*",
      permanent: false,
    }];
  },
};

export default nextConfig;
