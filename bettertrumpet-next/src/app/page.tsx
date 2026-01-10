"use client"

import { useState, useEffect } from "react"
import dynamic from "next/dynamic"

const AsciiVideoPlayer = dynamic(
  () => import("@/components/ascii-video").then((mod) => mod.AsciiVideoPlayer),
  { ssr: false }
)

export default function Home() {
  const [theme, setTheme] = useState<"dark" | "light">("dark")
  const [menuOpen, setMenuOpen] = useState(false)
  const [copyToast, setCopyToast] = useState(false)
  const [face, setFace] = useState("༼ つ ╹ ╹ ༽つ")
  const [currentVideo, setCurrentVideo] = useState(0)
  const [progress, setProgress] = useState(0)

  // Face blink animation
  useEffect(() => {
    const blink = () => {
      setFace("༼ つ - - ༽つ")
      setTimeout(() => setFace("༼ つ ╹ ╹ ༽つ"), 120)
    }

    const scheduleBlink = () => {
      const delay = 3000 + Math.random() * 5000
      setTimeout(() => {
        blink()
        if (Math.random() < 0.3) {
          setTimeout(blink, 250)
        }
        scheduleBlink()
      }, delay)
    }

    scheduleBlink()
  }, [])

  // Theme persistence
  useEffect(() => {
    const saved = localStorage.getItem("theme") as "dark" | "light" | null
    if (saved) setTheme(saved)
  }, [])

  const toggleTheme = () => {
    const newTheme = theme === "dark" ? "light" : "dark"
    setTheme(newTheme)
    localStorage.setItem("theme", newTheme)
  }

  const switchVideo = () => {
    setCurrentVideo((prev) => (prev + 1) % 2)
    setProgress(0)
  }

  const copyWinget = () => {
    navigator.clipboard.writeText("winget install bettertrumpet")
    setCopyToast(true)
    setTimeout(() => setCopyToast(false), 2000)
  }

  const isDark = theme === "dark"

  return (
    <div
      className={`min-h-screen flex items-center justify-center transition-colors duration-300 ${
        isDark ? "bg-[#0a0a0a] text-[#e0e0e0]" : "bg-white text-[#1e3a8a]"
      }`}
      style={{ fontFamily: "'JetBrains Mono', monospace" }}
    >
      {/* Theme toggle */}
      <button
        onClick={toggleTheme}
        className={`fixed top-6 right-6 w-10 h-10 rounded-full flex items-center justify-center transition-all duration-300 z-50 ${
          isDark ? "bg-[#1a1a1a] hover:bg-[#333]" : "bg-white/80 hover:bg-white border border-[#cbd5e1]"
        }`}
      >
        {isDark ? (
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
            <circle cx="12" cy="12" r="5" />
            <path d="M12 1v2M12 21v2M4.22 4.22l1.42 1.42M18.36 18.36l1.42 1.42M1 12h2M21 12h2M4.22 19.78l1.42-1.42M18.36 5.64l1.42-1.42" />
          </svg>
        ) : (
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
            <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z" />
          </svg>
        )}
      </button>

      <div className="grid grid-cols-1 lg:grid-cols-[1fr_1.3fr] gap-12 lg:gap-20 w-full max-w-6xl px-8 py-12 items-center">
        {/* Content */}
        <div className="flex flex-col justify-center z-10">
          <div className="text-xl mb-8 select-none">{face}</div>
          
          <h1 className="text-lg font-normal mb-6 lowercase tracking-tight">bettertrumpet</h1>
          
          <p className={`mb-8 text-sm leading-relaxed max-w-md ${isDark ? "text-[#888]" : "text-[#64748b]"}`}>
            a fork of eartrumpet. because the original was almost perfect.
          </p>

          <ul className={`mb-10 pl-6 text-sm space-y-2 ${isDark ? "border-l border-[#555] text-[#888]" : "border-l border-[#cbd5e1] text-[#64748b]"}`}>
            <li>- enhanced volume control</li>
            <li>- slick music overlay</li>
            <li>- cleaner interface</li>
            <li>- small improvements that matter</li>
          </ul>

          {/* Download */}
          <div className="relative">
            <div className="flex items-center gap-6">
              <button
                onClick={() => setMenuOpen(!menuOpen)}
                className={`flex items-center gap-2 pb-0.5 transition-all duration-200 lowercase ${
                  isDark 
                    ? "border-b border-[#555] hover:border-white hover:text-white" 
                    : "border-b border-[#cbd5e1] hover:border-[#1e3a8a]"
                }`}
              >
                download
                <span className={`text-xs transition-transform duration-200 ${menuOpen ? "rotate-180" : ""}`}>▼</span>
              </button>
              <span className={`text-xs ${isDark ? "text-[#555]" : "text-[#94a3b8]"}`}>windows 10/11</span>
            </div>

            {/* Dropdown */}
            <div
              className={`absolute top-full left-0 mt-3 min-w-[280px] py-2 transition-all duration-200 z-50 ${
                menuOpen ? "opacity-100 visible translate-y-0" : "opacity-0 invisible -translate-y-2"
              } ${isDark ? "bg-[#0a0a0a] border border-[#555]" : "bg-white border border-[#cbd5e1]"}`}
            >
              <a
                href="https://github.com/xammen/bettertrumpet/releases/latest/download/BetterTrumpet.exe"
                className={`block px-4 py-3 transition-colors ${
                  isDark ? "hover:bg-[#333]" : "hover:bg-[#f1f5f9]"
                }`}
              >
                <span className="block text-sm">bettertrumpet.exe</span>
                <span className={`text-xs ${isDark ? "text-[#555]" : "text-[#94a3b8]"}`}>direct download</span>
              </a>
              <button
                onClick={copyWinget}
                className={`w-full text-left px-4 py-3 transition-colors ${
                  isDark ? "hover:bg-[#333]" : "hover:bg-[#f1f5f9]"
                }`}
              >
                <span className="block text-sm">winget install bettertrumpet</span>
                <span className={`text-xs ${isDark ? "text-[#555]" : "text-[#94a3b8]"}`}>click to copy</span>
              </button>
              <a
                href="https://github.com/xammen/bettertrumpet"
                target="_blank"
                rel="noopener noreferrer"
                className={`block px-4 py-3 transition-colors ${
                  isDark ? "hover:bg-[#333]" : "hover:bg-[#f1f5f9]"
                }`}
              >
                <span className="block text-sm">github</span>
                <span className={`text-xs ${isDark ? "text-[#555]" : "text-[#94a3b8]"}`}>source code & releases</span>
              </a>
            </div>
          </div>

          <div className={`mt-16 text-xs ${isDark ? "text-[#555]" : "text-[#94a3b8]"}`}>
            fork of eartrumpet by xmn
          </div>
        </div>

        {/* Video with ASCII effect */}
        <div className="z-10">
          <div className={`relative overflow-hidden ${isDark ? "border border-[#555]" : "border border-[#cbd5e1]"}`}>
            <AsciiVideoPlayer
              video1Src="/menu.mp4"
              video2Src="/music.mp4"
              currentVideo={currentVideo}
              onVideoEnd={switchVideo}
              onProgress={setProgress}
              isDark={isDark}
            />
            
            {/* Progress bar */}
            <div className={`h-[3px] ${isDark ? "bg-[#333]" : "bg-[#e2e8f0]"}`}>
              <div
                className={`h-full transition-none ${isDark ? "bg-[#e0e0e0]" : "bg-[#3b82f6]"}`}
                style={{ width: `${progress}%` }}
              />
            </div>
          </div>
          
          {/* Indicators */}
          <div className="flex gap-2 justify-center mt-4">
            <span className={`w-1.5 h-1.5 transition-colors ${currentVideo === 0 ? (isDark ? "bg-[#e0e0e0]" : "bg-[#1e3a8a]") : (isDark ? "bg-[#555]" : "bg-[#cbd5e1]")}`} />
            <span className={`w-1.5 h-1.5 transition-colors ${currentVideo === 1 ? (isDark ? "bg-[#e0e0e0]" : "bg-[#1e3a8a]") : (isDark ? "bg-[#555]" : "bg-[#cbd5e1]")}`} />
          </div>
        </div>
      </div>

      {/* Toast */}
      <div
        className={`fixed bottom-8 left-1/2 -translate-x-1/2 px-4 py-2 text-xs transition-all duration-200 ${
          copyToast ? "opacity-100 visible translate-y-0" : "opacity-0 invisible translate-y-4"
        } ${isDark ? "bg-[#333] text-[#e0e0e0]" : "bg-[#1e3a8a] text-white"}`}
      >
        copied to clipboard
      </div>
    </div>
  )
}
