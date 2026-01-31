export type LoginResponse = {
  token: string;
  officerId: string;
  role: string;
};

export type HsCodeMatch = {
  hsCode: string;
  description: string;
  matchPercent: number;
  comment: string;
  subsections: {
    hsCode: string;
    title: string;
    notes: string;
  }[];
};

export type RecentHsCode = {
  hsCode: string;
  description: string;
};

export type HsCodeScanResponse = {
  matches: HsCodeMatch[];
  note?: string | null;
  recentHsCodes: RecentHsCode[];
};

const rawBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "https://localhost:5001";
const API_BASE_URL = rawBaseUrl.replace(/\/+$/, "");

export async function login(serviceNumberOrEmail: string, password: string): Promise<LoginResponse> {
  const response = await fetch(`${API_BASE_URL}/api/auth/login`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json"
    },
    body: JSON.stringify({ serviceNumberOrEmail, password })
  });

  if (!response.ok) {
    const message = await response.text();
    throw new Error(message || "Login failed.");
  }

  return response.json();
}

export async function scanHsCode(
  description: string,
  file?: File | null,
  signal?: AbortSignal
): Promise<HsCodeScanResponse> {
  const formData = new FormData();
  if (description.trim()) {
    formData.append("description", description.trim());
  }
  if (file) {
    formData.append("file", file);
  }

  const response = await fetch(`${API_BASE_URL}/api/hscode/scan`, {
    method: "POST",
    body: formData,
    signal
  });

  if (!response.ok) {
    const message = await response.text();
    throw new Error(message || "HS code scan failed.");
  }

  return response.json();
}

export async function fetchRecentHsCodes(): Promise<RecentHsCode[]> {
  const response = await fetch(`${API_BASE_URL}/api/hscode/recent`);
  if (!response.ok) {
    return [];
  }
  return response.json();
}
