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

type HsCodeScanJobStart = {
  jobId: string;
};

type HsCodeScanJobStatus =
  | { status: "pending" }
  | { status: "completed"; result: HsCodeScanResponse }
  | { status: "failed"; error?: string | null };

const rawBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "";
const API_BASE_URL = rawBaseUrl.replace(/\/+$/, "");

const withBase = (path: string) => (API_BASE_URL ? `${API_BASE_URL}${path}` : path);

export async function login(serviceNumberOrEmail: string, password: string): Promise<LoginResponse> {
  const response = await fetch(withBase(`/api/auth/login`), {
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

  const response = await fetch(withBase(`/api/hscode/scan`), {
    method: "POST",
    body: formData,
    signal
  });

  if (!response.ok) {
    const message = await response.text();
    throw new Error(message || "HS code scan failed.");
  }

  const startData = (await response.json()) as HsCodeScanJobStart;
  if (!startData?.jobId) {
    throw new Error("HS code scan failed to start.");
  }

  return pollHsCodeScan(startData.jobId, signal);
}

export async function fetchRecentHsCodes(): Promise<RecentHsCode[]> {
  const response = await fetch(withBase(`/api/hscode/recent`));
  if (!response.ok) {
    return [];
  }
  return response.json();
}

async function pollHsCodeScan(jobId: string, signal?: AbortSignal): Promise<HsCodeScanResponse> {
  while (true) {
    const response = await fetch(withBase(`/api/hscode/scan/${jobId}`), { signal });
    if (!response.ok) {
      const message = await response.text();
      throw new Error(message || "HS code scan failed.");
    }

    const status = (await response.json()) as HsCodeScanJobStatus;
    if (status.status === "completed") {
      return status.result;
    }
    if (status.status === "failed") {
      throw new Error(status.error || "HS code scan failed.");
    }

    await sleep(3000, signal);
  }
}

function sleep(delayMs: number, signal?: AbortSignal): Promise<void> {
  return new Promise((resolve, reject) => {
    const timer = window.setTimeout(() => resolve(), delayMs);
    if (signal) {
      if (signal.aborted) {
        window.clearTimeout(timer);
        reject(new DOMException("Aborted", "AbortError"));
        return;
      }
      signal.addEventListener(
        "abort",
        () => {
          window.clearTimeout(timer);
          reject(new DOMException("Aborted", "AbortError"));
        },
        { once: true }
      );
    }
  });
}
