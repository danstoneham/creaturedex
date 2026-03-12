import { NextRequest, NextResponse } from "next/server";

export const maxDuration = 300; // 5 minutes

const API_URL = process.env.API_URL || "http://localhost:5163";

export async function POST(request: NextRequest) {
  const body = await request.json();
  const cookie = request.headers.get("cookie") || "";

  const res = await fetch(`${API_URL}/api/admin/generate/batch`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      Cookie: cookie,
    },
    body: JSON.stringify(body),
  });

  const data = await res.json().catch(() => null);
  return NextResponse.json(data, { status: res.status });
}
