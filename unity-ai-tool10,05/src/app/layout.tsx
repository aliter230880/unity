import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "AliTerra AI — Unity Developer Assistant",
  description: "AI-powered Unity fullstack developer tool with Tool Use orchestration",
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en">
      <head>
        <link rel="preconnect" href="https://fonts.googleapis.com" />
        <link rel="preconnect" href="https://fonts.gstatic.com" crossOrigin="anonymous" />
        <link
          href="https://fonts.googleapis.com/css2?family=Inter:wght@300;400;500;600;700&family=JetBrains+Mono:wght@400;500&display=swap"
          rel="stylesheet"
        />
      </head>
      <body className="h-screen overflow-hidden">{children}</body>
    </html>
  );
}
