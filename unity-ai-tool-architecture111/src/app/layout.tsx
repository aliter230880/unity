import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "AliTerra AI — Unity Fullstack Developer",
  description: "AI-powered Unity development tool — reads, writes, and controls your entire Unity project",
};

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="ru">
      <body style={{ height: "100vh", overflow: "hidden" }}>
        {children}
      </body>
    </html>
  );
}
