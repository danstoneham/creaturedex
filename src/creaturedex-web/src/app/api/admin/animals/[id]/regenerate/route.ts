import { NextRequest, NextResponse } from "next/server";

export const maxDuration = 300; // 5 minutes

const API_URL = process.env.API_URL || "http://localhost:5163";

export async function POST(
  request: NextRequest,
  { params }: { params: Promise<{ id: string }> }
) {
  const { id } = await params;
  const cookie = request.headers.get("cookie") || "";

  const res = await fetch(`${API_URL}/api/admin/animals/${id}/regenerate`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      Cookie: cookie,
    },
  });

  const data = await res.json().catch(() => null);
  return NextResponse.json(data, { status: res.status });
}
