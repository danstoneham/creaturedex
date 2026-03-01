import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  async rewrites() {
    return [
      {
        source: "/api/:path*",
        destination: "http://localhost:5032/api/:path*",
      },
      {
        source: "/health/:path*",
        destination: "http://localhost:5032/health/:path*",
      },
    ];
  },
};

export default nextConfig;
