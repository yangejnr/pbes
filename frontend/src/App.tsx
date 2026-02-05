import { useEffect, useMemo, useRef, useState, type FormEvent, type ReactNode } from "react";
import logo from "./assets/logo.png";
import { fetchRecentHsCodes, scanHsCode, type HsCodeMatch, type RecentHsCode } from "./services/api";

const featureCards: { title: string; description: string; icon: ReactNode }[] = [
  {
    title: "Passenger Risk Screening",
    description: "Identify high-risk profiles with structured checks and alerts.",
    icon: (
      <svg viewBox="0 0 24 24" className="h-5 w-5" fill="none" stroke="currentColor" strokeWidth="1.8">
        <path d="M12 3l7 3v6c0 4.4-3 7.5-7 9-4-1.5-7-4.6-7-9V6l7-3z" />
        <path d="M9.5 12.5l2 2 4-4" />
      </svg>
    )
  },
  {
    title: "Baggage Examination",
    description: "Capture inspection outcomes with clear status updates.",
    icon: (
      <svg viewBox="0 0 24 24" className="h-5 w-5" fill="none" stroke="currentColor" strokeWidth="1.8">
        <rect x="4" y="7" width="16" height="12" rx="2" />
        <path d="M8 7V5a4 4 0 0 1 8 0v2" />
        <path d="M9 13h6" />
      </svg>
    )
  },
  {
    title: "Incident Reporting",
    description: "Log violations, seizures, and handoffs with timestamps.",
    icon: (
      <svg viewBox="0 0 24 24" className="h-5 w-5" fill="none" stroke="currentColor" strokeWidth="1.8">
        <path d="M12 7v6" />
        <path d="M12 17h.01" />
        <path d="M10.3 4.5h3.4l6 10.4a1.5 1.5 0 0 1-1.3 2.2H5.6a1.5 1.5 0 0 1-1.3-2.2l6-10.4z" />
      </svg>
    )
  },
  {
    title: "Compliance Records",
    description: "Generate audit-ready records for supervisory review.",
    icon: (
      <svg viewBox="0 0 24 24" className="h-5 w-5" fill="none" stroke="currentColor" strokeWidth="1.8">
        <path d="M7 4h7l4 4v12a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2z" />
        <path d="M14 4v4h4" />
        <path d="M8 13h8" />
        <path d="M8 17h6" />
      </svg>
    )
  }
];

function isDescriptionSpecific(description: string): boolean {
  const words = description.trim().split(/\s+/).filter(Boolean);
  return words.length >= 5 && description.trim().length >= 25;
}

function isGoodsRelatedDescription(description: string): boolean {
  const text = description.toLowerCase();
  const blockedPhrases = [
    "weather",
    "football",
    "soccer",
    "match",
    "scores",
    "news",
    "politic",
    "election",
    "president",
    "governor",
    "import duty",
    "customs duty",
    "tariff",
    "tax rate",
    "exchange rate",
    "visa",
    "passport"
  ];

  if (blockedPhrases.some((phrase) => text.includes(phrase))) {
    return false;
  }

  return true;
}

async function checkImageClarity(
  file: File,
  source: "upload" | "camera" = "upload"
): Promise<{ ok: boolean; message?: string }> {
  if (!file.type.startsWith("image/")) {
    return { ok: true };
  }

  const minSize = source === "camera" ? 60 * 1024 : 80 * 1024;
  if (file.size < minSize) {
    return { ok: false, message: "Image appears too small. Upload a clearer photo with more detail." };
  }

  const imageBitmap = await createImageBitmap(file);
  const minWidth = source === "camera" ? 640 : 900;
  const minHeight = source === "camera" ? 480 : 600;
  if (imageBitmap.width < minWidth || imageBitmap.height < minHeight) {
    return { ok: false, message: "Image resolution is low. Please upload a clearer image." };
  }

  const canvas = document.createElement("canvas");
  canvas.width = 300;
  canvas.height = Math.round((imageBitmap.height / imageBitmap.width) * 300);
  const context = canvas.getContext("2d");
  if (!context) {
    return { ok: true };
  }

  context.drawImage(imageBitmap, 0, 0, canvas.width, canvas.height);
  const { data, width, height } = context.getImageData(0, 0, canvas.width, canvas.height);
  const grayscale = new Float32Array(width * height);

  for (let i = 0; i < data.length; i += 4) {
    const r = data[i];
    const g = data[i + 1];
    const b = data[i + 2];
    grayscale[i / 4] = 0.299 * r + 0.587 * g + 0.114 * b;
  }

  const laplacian = new Float32Array(width * height);
  for (let y = 1; y < height - 1; y += 1) {
    for (let x = 1; x < width - 1; x += 1) {
      const idx = y * width + x;
      const value =
        grayscale[idx - width] +
        grayscale[idx + width] +
        grayscale[idx - 1] +
        grayscale[idx + 1] -
        4 * grayscale[idx];
      laplacian[idx] = value;
    }
  }

  const mean = laplacian.reduce((sum, val) => sum + val, 0) / laplacian.length;
  const variance = laplacian.reduce((sum, val) => sum + (val - mean) ** 2, 0) / laplacian.length;

  if (variance < 120) {
    return { ok: false, message: "Image looks blurry. Please upload a clearer photo with more detail." };
  }

  return { ok: true };
}

export default function App() {
  const [isScanOpen, setIsScanOpen] = useState(false);
  const [description, setDescription] = useState("");
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [previewUrl, setPreviewUrl] = useState<string | null>(null);
  const [scanError, setScanError] = useState<string | null>(null);
  const [scanResults, setScanResults] = useState<HsCodeMatch[] | null>(null);
  const [scanNote, setScanNote] = useState<string | null>(null);
  const [recentHsCodes, setRecentHsCodes] = useState<RecentHsCode[]>([]);
  const [isScanning, setIsScanning] = useState(false);
  const [isSlowScan, setIsSlowScan] = useState(false);
  const [selectedResult, setSelectedResult] = useState<HsCodeMatch | null>(null);
  const [isCameraOpen, setIsCameraOpen] = useState(false);
  const [cameraError, setCameraError] = useState<string | null>(null);
  const videoRef = useRef<HTMLVideoElement | null>(null);
  const streamRef = useRef<MediaStream | null>(null);
  const abortRef = useRef<AbortController | null>(null);

  const fileLabel = useMemo(() => {
    if (!selectedFile) return "No file selected";
    return `${selectedFile.name} · ${(selectedFile.size / 1024).toFixed(0)} KB`;
  }, [selectedFile]);

  const handleFileSelect = async (file: File | null, source: "upload" | "camera" = "upload") => {
    setScanError(null);
    setScanResults(null);
    setScanNote(null);
    setCameraError(null);

    if (!file) {
      setSelectedFile(null);
      setPreviewUrl(null);
      return;
    }

    const allowedTypes = ["application/pdf", "image/jpeg", "image/png"];
    if (!allowedTypes.includes(file.type)) {
      setScanError("Only PDF, JPEG, JPG, or PNG files are allowed.");
      return;
    }

    const clarity = await checkImageClarity(file, source);
    if (!clarity.ok) {
      setScanError(clarity.message ?? "Please upload a clearer image.");
      return;
    }

    if (previewUrl) {
      URL.revokeObjectURL(previewUrl);
    }

    if (file.type.startsWith("image/")) {
      setPreviewUrl(URL.createObjectURL(file));
    } else {
      setPreviewUrl(null);
    }

    setSelectedFile(file);
  };

  const stopCamera = () => {
    if (streamRef.current) {
      streamRef.current.getTracks().forEach((track) => track.stop());
      streamRef.current = null;
    }
    if (videoRef.current) {
      videoRef.current.srcObject = null;
    }
    setIsCameraOpen(false);
  };

  const startCamera = async () => {
    setCameraError(null);
    setScanError(null);

    if (!navigator.mediaDevices?.getUserMedia) {
      setCameraError("Camera access is not supported on this device.");
      return;
    }

    try {
      const stream = await navigator.mediaDevices.getUserMedia({
        video: {
          facingMode: { ideal: "environment" },
          width: { ideal: 1280 },
          height: { ideal: 720 }
        }
      });
      streamRef.current = stream;
      setIsCameraOpen(true);
    } catch (error) {
      setCameraError("Unable to access camera. Please check permissions.");
    }
  };

  const capturePhoto = async () => {
    if (!videoRef.current) return;
    const video = videoRef.current;

    if (video.videoWidth < 2 || video.videoHeight < 2) {
      setCameraError("Camera is still loading. Please wait a moment and try again.");
      return;
    }

    const track = streamRef.current?.getVideoTracks()[0];
    const settings = track?.getSettings();
    const width = video.videoWidth || settings?.width || 1280;
    const height = video.videoHeight || settings?.height || 720;

    const canvas = document.createElement("canvas");
    canvas.width = width;
    canvas.height = height;
    const context = canvas.getContext("2d");
    if (!context) return;

    context.drawImage(video, 0, 0, canvas.width, canvas.height);

    canvas.toBlob(async (blob) => {
      if (!blob) return;
      const file = new File([blob], "camera-capture.png", { type: "image/png" });
      await handleFileSelect(file, "camera");
      stopCamera();
    }, "image/png");
  };

  useEffect(() => {
    if (!isScanOpen) {
      stopCamera();
    }
    return () => stopCamera();
  }, [isScanOpen]);

  useEffect(() => {
    if (isCameraOpen && videoRef.current && streamRef.current) {
      videoRef.current.srcObject = streamRef.current;
      void videoRef.current.play().catch(() => undefined);
    }
  }, [isCameraOpen]);

  useEffect(() => {
    if (!isScanning) {
      setIsSlowScan(false);
      return;
    }

    const timer = window.setTimeout(() => setIsSlowScan(true), 12000);
    return () => window.clearTimeout(timer);
  }, [isScanning]);

  const handleScan = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setScanError(null);
    setScanResults(null);
    setScanNote(null);
    setIsSlowScan(false);
    setSelectedResult(null);

    const hasDescription = description.trim().length > 0;
    const hasFile = Boolean(selectedFile);

    if (!hasDescription && !hasFile) {
      setScanError("Provide a detailed description or upload a clear image to begin.");
      return;
    }

    if (hasDescription && !isGoodsRelatedDescription(description)) {
      setScanError("This tool only supports HS code classification for goods. Please provide a specific item description.");
      return;
    }

    if (hasDescription && !isDescriptionSpecific(description)) {
      setScanError("Please provide a more specific description (material, use, size, brand, etc.).");
      return;
    }

    setIsScanning(true);
    const controller = new AbortController();
    abortRef.current = controller;
    try {
      const response = await scanHsCode(description, selectedFile, controller.signal);
      setScanResults(response.matches);
      setScanNote(response.note ?? null);
      setRecentHsCodes(response.recentHsCodes ?? []);
      setSelectedResult(null);
    } catch (error) {
      if ((error as Error)?.name === "AbortError") {
        setScanError("Scan cancelled.");
      } else {
        const message = error instanceof Error ? error.message : "HS code scan failed.";
        setScanError(message);
      }
    } finally {
      setIsScanning(false);
      abortRef.current = null;
    }
  };

  const closeModal = () => {
    setIsScanOpen(false);
    setScanError(null);
    setScanResults(null);
    setScanNote(null);
    setCameraError(null);
    setIsSlowScan(false);
    setSelectedResult(null);
    abortRef.current?.abort();
    if (previewUrl) {
      URL.revokeObjectURL(previewUrl);
    }
    setPreviewUrl(null);
    setSelectedFile(null);
  };

  useEffect(() => {
    fetchRecentHsCodes()
      .then((data) => setRecentHsCodes(data))
      .catch(() => undefined);
  }, []);

  return (
    <div className="min-h-screen bg-ncsLight text-ncsInk">
      <header className="border-b border-ncsBorder bg-white">
        <div className="mx-auto flex w-full max-w-6xl items-center justify-between gap-6 px-6 py-4">
          <div className="flex items-center gap-3">
            <img src={logo} alt="Nigeria Customs Service" className="h-12 w-12 object-contain" />
            <div>
              <p className="text-sm font-semibold text-ncsGreen">Nigeria Customs Service</p>
              <p className="text-[11px] uppercase tracking-[0.25em] text-neutral-500">
                HS Code Intelligence Tool
              </p>
            </div>
          </div>
          <nav className="hidden items-center gap-8 text-sm text-neutral-600 md:flex">
            <a className="transition hover:text-ncsGreen" href="#home">Home</a>
            <button className="transition hover:text-ncsGreen" onClick={() => setIsScanOpen(true)}>
              Start HS Code Scan
            </button>
            <a className="transition hover:text-ncsGreen" href="#contact">Contact</a>
          </nav>
        </div>
      </header>

      <main id="home" className="mx-auto w-full max-w-6xl px-6 pb-20">
        <section className="relative grid gap-10 pb-16 pt-12 md:grid-cols-[1.1fr_0.9fr]">
          <div className="map-bg" aria-hidden="true" />
          <div className="relative z-10 space-y-5">
            <p className="text-xs font-semibold uppercase tracking-[0.3em] text-neutral-500">
              NCS HS Code Intelligence Tool
            </p>
            <h1 className="font-serif text-4xl text-ncsGreen md:text-5xl">
              NCS HS CODE INTELLIGENCE TOOL
            </h1>
            <p className="max-w-xl text-base text-neutral-600">
              AI-powered HS code classification to support Nigeria Customs Service officers with
              accurate matching, compliance checks, and audit-ready reporting.
            </p>
            <button
              className="rounded-full border-2 border-ncsGreen px-6 py-3 text-sm font-semibold text-ncsGreen transition hover:bg-ncsGreen hover:text-white"
              onClick={() => setIsScanOpen(true)}
            >
              Start HS Code Scan
            </button>
          </div>
          <div className="relative z-10 flex items-center justify-center">
            <div className="w-full rounded-3xl border border-ncsBorder bg-white p-6 shadow-card">
              <div className="flex items-center justify-between text-xs uppercase tracking-widest text-neutral-500">
                <span>Examination Summary</span>
                <span className="rounded-full bg-[#e8f4eb] px-3 py-1 text-[10px] text-ncsGreen">Live</span>
              </div>
              <div className="mt-6 rounded-2xl border border-ncsBorder bg-ncsLight p-4 text-xs text-neutral-600">
                <p className="text-[11px] font-semibold uppercase tracking-widest text-neutral-500">
                  Last 10 HS Codes
                </p>
                <ul className="mt-3 grid gap-2 text-xs text-neutral-600">
                  {recentHsCodes.length === 0 ? (
                    <li className="text-neutral-500">No scans recorded yet.</li>
                  ) : (
                    recentHsCodes.map((item, index) => (
                      <li key={`${item.hsCode}-${index}`} className="flex items-start justify-between gap-3">
                        <div>
                          <span className="font-semibold text-ncsInk">{item.hsCode}</span>
                          <p className="mt-1 text-[11px] text-neutral-500">{item.description}</p>
                        </div>
                        <span className="text-neutral-500">#{index + 1}</span>
                      </li>
                    ))
                  )}
                </ul>
              </div>
            </div>
          </div>
        </section>

        <section className="grid gap-6 pb-12 md:grid-cols-4">
          {featureCards.map((card) => (
            <div key={card.title} className="rounded-2xl border border-ncsBorder bg-white p-6 shadow-card">
              <div className="flex h-12 w-12 items-center justify-center rounded-full bg-[#eaf4ed] text-ncsGreen">
                {card.icon}
              </div>
              <h3 className="mt-4 text-base font-semibold text-ncsInk">{card.title}</h3>
              <p className="mt-2 text-sm text-neutral-600">{card.description}</p>
            </div>
          ))}
        </section>

        <section id="contact" className="border-t border-ncsBorder py-6" />
      </main>

      <footer className="fixed bottom-0 left-0 right-0 border-t border-ncsBorder bg-white">
        <div className="mx-auto flex w-full max-w-6xl flex-wrap items-center justify-between gap-4 px-6 py-4 text-sm text-neutral-500">
          <div>
            <span className="font-semibold text-ncsInk">NCS HS Code Intelligence Tool</span> · Nigeria Customs Service
          </div>
          <div className="flex gap-6">
            <a className="transition hover:text-ncsInk" href="#home">Home</a>
            <a className="transition hover:text-ncsInk" href="#contact">Contact</a>
          </div>
        </div>
      </footer>

      {isScanOpen ? (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 px-4"
          onClick={closeModal}
        >
          <div
            className="w-full max-w-4xl rounded-2xl border border-ncsBorder bg-white shadow-card"
            onClick={(event) => event.stopPropagation()}
          >
            <div className="flex items-center justify-between border-b border-ncsBorder px-6 py-4">
              <h3 className="font-serif text-lg text-ncsGreen">Start HS Code Scan</h3>
              <button
                className="text-xs font-semibold uppercase tracking-widest text-neutral-500 transition hover:text-ncsInk"
                onClick={closeModal}
              >
                Close
              </button>
            </div>
            <div className="max-h-[80vh] overflow-y-auto px-6 py-6">
              <p className="text-sm text-neutral-600">
                Provide a detailed item description and/or a clear image to match HS codes.
              </p>
              <form className="mt-5 space-y-5" onSubmit={handleScan}>
                <div>
                  <label className="text-xs font-semibold uppercase tracking-wide text-neutral-500">
                    Item description
                  </label>
                  <textarea
                    className="mt-2 min-h-[120px] w-full rounded-lg border border-ncsBorder bg-white px-4 py-3 text-sm outline-none transition focus:border-ncsGreen"
                    placeholder="e.g. 2kg stainless steel pressure cooker with glass lid, electric, 220V"
                    value={description}
                    onChange={(event) => setDescription(event.target.value)}
                  />
                  <p className="mt-2 text-xs text-neutral-500">
                    Be specific: include material, use, size, brand, and any distinguishing features.
                  </p>
                </div>

                <div className="grid gap-4 md:grid-cols-2">
                  <div>
                    <label className="text-xs font-semibold uppercase tracking-wide text-neutral-500">
                      Upload item image (PDF/JPEG/PNG)
                    </label>
                    <input
                      type="file"
                      accept=".pdf,image/jpeg,image/png"
                      className="mt-2 w-full rounded-lg border border-ncsBorder bg-white px-4 py-3 text-sm text-neutral-600"
                      onChange={(event) => handleFileSelect(event.target.files?.[0] ?? null)}
                    />
                  </div>
                  <div>
                    <label className="text-xs font-semibold uppercase tracking-wide text-neutral-500">
                      Use camera (image only)
                    </label>
                    <div className="mt-2 rounded-lg border border-ncsBorder bg-white p-3">
                      {isCameraOpen ? (
                        <div className="space-y-3">
                          <video ref={videoRef} autoPlay playsInline muted className="h-40 w-full rounded-lg object-cover" />
                          <div className="flex items-center gap-3">
                            <button
                              type="button"
                              className="rounded-full bg-ncsGreen px-4 py-2 text-xs font-semibold text-white transition hover:bg-ncsGreenDark"
                              onClick={capturePhoto}
                            >
                              Capture Photo
                            </button>
                            <button
                              type="button"
                              className="rounded-full border border-ncsBorder px-4 py-2 text-xs font-semibold text-ncsInk"
                              onClick={stopCamera}
                            >
                              Stop Camera
                            </button>
                          </div>
                        </div>
                      ) : (
                        <div className="space-y-3">
                          <div className="rounded-lg border border-dashed border-ncsBorder bg-ncsLight px-3 py-6 text-center text-xs text-neutral-500">
                            Camera is off. Start the camera to capture an item image.
                          </div>
                          <button
                            type="button"
                            className="rounded-full border border-ncsGreen px-4 py-2 text-xs font-semibold text-ncsGreen transition hover:bg-ncsGreen hover:text-white"
                            onClick={startCamera}
                          >
                            Start Camera
                          </button>
                        </div>
                      )}
                    </div>
                  </div>
                </div>

                <div className="rounded-lg border border-ncsBorder bg-ncsLight px-4 py-3 text-xs text-neutral-600">
                  Selected file: {fileLabel}
                </div>

                {previewUrl ? (
                  <div className="rounded-2xl border border-ncsBorder bg-white p-4">
                    <img
                      src={previewUrl}
                      alt="Selected item preview"
                      className="h-48 w-full rounded-xl object-cover"
                    />
                  </div>
                ) : null}

                {scanError ? (
                  <div className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700">
                    {scanError}
                  </div>
                ) : null}

                {cameraError ? (
                  <div className="rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-700">
                    {cameraError}
                  </div>
                ) : null}

                {scanNote ? (
                  <div className="rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-700">
                    {scanNote}
                  </div>
                ) : null}

                {isSlowScan ? (
                  <div className="rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-700">
                    This scan is taking longer than usual. Consider using a smaller image or a more specific description.
                  </div>
                ) : null}

                <button
                  type="submit"
                  disabled={isScanning}
                  className="w-full rounded-full bg-ncsGreen px-6 py-3 text-sm font-semibold text-white shadow-card transition hover:bg-ncsGreenDark disabled:cursor-not-allowed disabled:opacity-70"
                >
                  {isScanning ? "Scanning..." : "Scan for HS Code"}
                </button>
                {isScanning ? (
                  <button
                    type="button"
                    className="w-full rounded-full border border-ncsBorder px-6 py-3 text-xs font-semibold text-ncsInk transition hover:border-ncsGreen"
                    onClick={() => abortRef.current?.abort()}
                  >
                    Cancel Scan
                  </button>
                ) : null}
              </form>

              {scanResults ? (
                <div className="mt-6 space-y-4">
                  <h4 className="text-sm font-semibold text-ncsInk">HS Code Matches</h4>
                  <div className="overflow-x-auto rounded-2xl border border-ncsBorder">
                    <table className="w-full text-left text-sm text-neutral-600">
                      <thead className="bg-ncsLight text-xs uppercase tracking-wide text-neutral-500">
                        <tr>
                          <th className="px-4 py-3">HS Code</th>
                          <th className="px-4 py-3">HS Code Description</th>
                          <th className="px-4 py-3">Match %</th>
                          <th className="px-4 py-3">Action</th>
                        </tr>
                      </thead>
                      <tbody>
                        {scanResults.map((result) => (
                          <tr key={result.hsCode} className="border-t border-ncsBorder">
                            <td className="px-4 py-3 font-semibold text-ncsInk">{result.hsCode}</td>
                            <td className="px-4 py-3">{result.description}</td>
                            <td className="px-4 py-3 font-semibold text-ncsGreen">
                              {Math.round(result.matchPercent)}%
                            </td>
                            <td className="px-4 py-3">
                              <button
                                className="rounded-full border border-ncsGreen px-3 py-1 text-xs font-semibold text-ncsGreen transition hover:bg-ncsGreen hover:text-white"
                                onClick={() => setSelectedResult(result)}
                              >
                                View Details
                              </button>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </div>
              ) : null}
            </div>
          </div>
        </div>
      ) : null}

      {selectedResult ? (
        <div
          className="fixed inset-0 z-[60] flex items-center justify-center bg-black/50 px-4"
          onClick={() => setSelectedResult(null)}
        >
          <div
            className="w-full max-w-3xl rounded-2xl border border-ncsBorder bg-white shadow-card"
            onClick={(event) => event.stopPropagation()}
          >
            <div className="flex items-center justify-between border-b border-ncsBorder px-6 py-4">
              <div>
                <p className="text-xs uppercase tracking-wide text-neutral-500">HS Code Details</p>
                <h3 className="mt-1 font-serif text-xl text-ncsGreen">{selectedResult.hsCode}</h3>
              </div>
              <button
                className="text-xs font-semibold uppercase tracking-widest text-neutral-500 transition hover:text-ncsInk"
                onClick={() => setSelectedResult(null)}
              >
                Close
              </button>
            </div>
            <div className="max-h-[75vh] overflow-y-auto px-6 py-6 text-sm text-neutral-600">
              <div className="grid gap-4">
                <div>
                  <span className="text-xs font-semibold uppercase tracking-wide text-neutral-500">Description</span>
                  <p className="mt-1">{selectedResult.description}</p>
                </div>
                <div>
                  <span className="text-xs font-semibold uppercase tracking-wide text-neutral-500">Match %</span>
                  <p className="mt-1 font-semibold text-ncsGreen">{Math.round(selectedResult.matchPercent)}%</p>
                </div>
                <div>
                  <span className="text-xs font-semibold uppercase tracking-wide text-neutral-500">Comment</span>
                  <p className="mt-1">{selectedResult.comment}</p>
                </div>
                <div>
                  <span className="text-xs font-semibold uppercase tracking-wide text-neutral-500">Sub-sections</span>
                  {selectedResult.subsections?.length ? (
                    <div className="mt-3 space-y-3">
                      {selectedResult.subsections.map((section) => (
                        <div key={section.hsCode} className="rounded-xl border border-ncsBorder bg-ncsLight p-3">
                          <div className="flex items-start justify-between gap-4">
                            <div>
                              <p className="font-semibold text-ncsInk">{section.hsCode}</p>
                              <p className="text-sm text-ncsInk">{section.title}</p>
                            </div>
                          </div>
                          <p className="mt-2 text-xs text-neutral-500">{section.notes}</p>
                        </div>
                      ))}
                    </div>
                  ) : (
                    <p className="mt-2 text-xs text-neutral-500">No sub-sections returned.</p>
                  )}
                </div>
              </div>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  );
}
