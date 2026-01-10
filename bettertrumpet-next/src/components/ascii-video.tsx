"use client"

import { useState, useEffect, useRef } from "react"
import { Canvas, useFrame, useThree } from "@react-three/fiber"
import { EffectComposer } from "@react-three/postprocessing"
import { Vector2, VideoTexture, PlaneGeometry, MeshBasicMaterial, Mesh } from "three"
import { AsciiEffect } from "./ascii-effect"

interface VideoPlaneProps {
  videoElement: HTMLVideoElement | null
}

function VideoPlane({ videoElement }: VideoPlaneProps) {
  const meshRef = useRef<Mesh>(null)
  const { viewport } = useThree()

  useFrame(() => {
    if (meshRef.current && videoElement) {
      // Update texture
      const material = meshRef.current.material as MeshBasicMaterial
      if (material.map) {
        material.map.needsUpdate = true
      }
    }
  })

  useEffect(() => {
    if (meshRef.current && videoElement) {
      const texture = new VideoTexture(videoElement)
      const material = meshRef.current.material as MeshBasicMaterial
      material.map = texture
      material.needsUpdate = true
    }
  }, [videoElement])

  // Calculate aspect ratio to fill the canvas properly
  const videoAspect = 16 / 9 // Assuming 16:9 video
  const viewportAspect = viewport.width / viewport.height
  
  let planeWidth = viewport.width
  let planeHeight = viewport.height
  
  if (viewportAspect > videoAspect) {
    planeHeight = viewport.width / videoAspect
  } else {
    planeWidth = viewport.height * videoAspect
  }

  return (
    <mesh ref={meshRef}>
      <planeGeometry args={[planeWidth, planeHeight]} />
      <meshBasicMaterial />
    </mesh>
  )
}

interface AsciiVideoPlayerProps {
  video1Src: string
  video2Src: string
  currentVideo: number
  onVideoEnd: () => void
  onProgress: (progress: number) => void
  isDark: boolean
}

export function AsciiVideoPlayer({
  video1Src,
  video2Src,
  currentVideo,
  onVideoEnd,
  onProgress,
  isDark
}: AsciiVideoPlayerProps) {
  const containerRef = useRef<HTMLDivElement>(null)
  const video1Ref = useRef<HTMLVideoElement>(null)
  const video2Ref = useRef<HTMLVideoElement>(null)
  const [resolution, setResolution] = useState(new Vector2(800, 450))
  const [activeVideo, setActiveVideo] = useState<HTMLVideoElement | null>(null)
  const animationRef = useRef<number>(null)

  // Handle resolution
  useEffect(() => {
    const updateResolution = () => {
      if (containerRef.current) {
        const rect = containerRef.current.getBoundingClientRect()
        setResolution(new Vector2(rect.width, rect.height))
      }
    }
    updateResolution()
    window.addEventListener("resize", updateResolution)
    return () => window.removeEventListener("resize", updateResolution)
  }, [])

  // Handle video switching
  useEffect(() => {
    const video = currentVideo === 0 ? video1Ref.current : video2Ref.current
    const otherVideo = currentVideo === 0 ? video2Ref.current : video1Ref.current
    
    if (video) {
      if (otherVideo) {
        otherVideo.pause()
        otherVideo.currentTime = 0
      }
      video.currentTime = 0
      video.play()
      setActiveVideo(video)
    }
  }, [currentVideo])

  // Progress tracking
  useEffect(() => {
    const updateProgress = () => {
      const video = currentVideo === 0 ? video1Ref.current : video2Ref.current
      if (video && video.duration) {
        onProgress((video.currentTime / video.duration) * 100)
      }
      animationRef.current = requestAnimationFrame(updateProgress)
    }
    animationRef.current = requestAnimationFrame(updateProgress)
    return () => {
      if (animationRef.current) cancelAnimationFrame(animationRef.current)
    }
  }, [currentVideo, onProgress])

  return (
    <div ref={containerRef} className="relative w-full aspect-video">
      {/* Hidden video elements */}
      <video
        ref={video1Ref}
        src={video1Src}
        muted
        playsInline
        onEnded={onVideoEnd}
        className="hidden"
        crossOrigin="anonymous"
      />
      <video
        ref={video2Ref}
        src={video2Src}
        muted
        playsInline
        onEnded={onVideoEnd}
        className="hidden"
        crossOrigin="anonymous"
      />

      {/* Three.js Canvas with ASCII effect */}
      <Canvas
        camera={{ position: [0, 0, 5], fov: 50 }}
        className="w-full h-full"
        style={{ background: isDark ? "#000" : "#fff" }}
      >
        <color attach="background" args={[isDark ? "#000" : "#fff"]} />
        
        <VideoPlane videoElement={activeVideo} />

        <EffectComposer>
          <AsciiEffect
            style="standard"
            cellSize={3}
            invert={!isDark}
            color={true}
            resolution={resolution}
            mousePos={new Vector2(0, 0)}
            postfx={{
              scanlineIntensity: 0.15,
              scanlineCount: 100,
              targetFPS: 0,
              jitterIntensity: 0,
              jitterSpeed: 1,
              mouseGlowEnabled: false,
              mouseGlowRadius: 200,
              mouseGlowIntensity: 1.5,
              vignetteIntensity: 0.3,
              vignetteRadius: 1.5,
              colorPalette: 0,
              curvature: 0,
              aberrationStrength: 0,
              noiseIntensity: 0,
              noiseScale: 1,
              noiseSpeed: 1,
              waveAmplitude: 0,
              waveFrequency: 10,
              waveSpeed: 1,
              glitchIntensity: 0,
              glitchFrequency: 0,
              brightnessAdjust: 0.1,
              contrastAdjust: 1.2,
            }}
          />
        </EffectComposer>
      </Canvas>
    </div>
  )
}
