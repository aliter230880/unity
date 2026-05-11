import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "AliTerra AI — Unity Fullstack Developer",
  description:
    "AI agent that sees, reads, writes and controls your Unity project in real time",
};

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="en" className="dark">
      <body>{children}</body>
    </html>
  );
}
