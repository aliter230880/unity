import type { Metadata } from "next";
import type { ReactNode } from "react";
import "./globals.css";

export const metadata: Metadata = {
  title: "Arena Next.js PostgreSQL Starter",
  description: "Starter template with Next.js, Drizzle, and PostgreSQL.",
};

import Link from 'next/link';

export default function RootLayout({ children }: { children: ReactNode }) {
  return (
    <html lang="en">
      <body className="bg-slate-100 text-slate-900 antialiased">
        <nav className="bg-white border-b px-8 py-4 flex gap-6 items-center">
          <Link href="/" className="font-bold text-xl text-blue-600">Unity AI Bridge</Link>
          <Link href="/" className="text-gray-600 hover:text-blue-600">Dashboard</Link>
          <Link href="/docs" className="text-gray-600 hover:text-blue-600">Documentation</Link>
        </nav>
        <main>{children}</main>
      </body>
    </html>
  );
}
