"use client"

import { useState, useEffect, useRef } from "react"
import { Canvas, useFrame } from "@react-three/fiber"
import { EffectComposer } from "@react-three/postprocessing"
import { Vector2, Mesh } from "three"
import { AsciiEffect } from "./ascii-effect"

function RotatingTorus() {
  const meshRef = useRef<Mesh>(null)
  
  useFrame((state) => {
    if (meshRef.current) {
      meshRef.current.rotation.x = state.clock.elapsedTime * 0.3
      meshRef.current.rotation.y = state.clock.elapsedTime * 0.5
    }
  })

  return (
    <mesh ref={meshRef}>
      <torusKnotGeometry args={[1, 0.3, 128, 32]} />
      <meshStandardMaterial color="#8866ff" metalness={0.5} roughness={0.2} />
    </mesh>
  )
}

function FloatingSpheres() {
  const group = useRef<THREE.Group>(null)
  
  useFrame((state) => {
    if (group.current) {
      group.current.rotation.y = state.clock.elapsedTime * 0.2
    }
  })

  return (
    <group ref={group}>
      {[...Array(6)].map((_, i) => {
        const angle = (i / 6) * Math.PI * 2
        const radius = 2.5
        return (
          <mesh key={i} position={[Math.cos(angle) * radius, Math.sin(angle) * radius, 0]}>
            <sphereGeometry args={[0.3, 32, 32]} />
            <meshStandardMaterial color="#44aaff" metalness={0.8} roughness={0.1} />
          </mesh>
        )
      })}
    </group>
  )
}

export function EffectScene() {
  const containerRef = useRef<HTMLDivElement>(null)
  const [mousePos, setMousePos] = useState(new Vector2(0, 0))
  const [resolution, setResolution] = useState(new Vector2(1920, 1080))

  useEffect(() => {
    const handleMouseMove = (e: MouseEvent) => {
      if (containerRef.current) {
        const rect = containerRef.current.getBoundingClientRect()
        const x = e.clientX - rect.left
        const y = rect.height - (e.clientY - rect.top)
        setMousePos(new Vector2(x, y))
      }
    }

    const container = containerRef.current
    if (container) {
      container.addEventListener("mousemove", handleMouseMove)

      const rect = container.getBoundingClientRect()
      setResolution(new Vector2(rect.width, rect.height))

      const handleResize = () => {
        const rect = container.getBoundingClientRect()
        setResolution(new Vector2(rect.width, rect.height))
      }
      window.addEventListener("resize", handleResize)

      return () => {
        container.removeEventListener("mousemove", handleMouseMove)
        window.removeEventListener("resize", handleResize)
      }
    }
  }, [])

  return (
    <div ref={containerRef} className="fixed inset-0 -z-10">
      <Canvas
        camera={{ position: [0, 0, 5], fov: 50 }}
        style={{ background: "#000000" }}
      >
        <color attach="background" args={["#000000"]} />
        
        <ambientLight intensity={0.5} />
        <pointLight position={[10, 10, 10]} intensity={1} />
        <pointLight position={[-10, -10, -10]} intensity={0.5} color="#4466ff" />
        
        <RotatingTorus />
        <FloatingSpheres />

        <EffectComposer>
          <AsciiEffect
            style="standard"
            cellSize={4}
            invert={true}
            color={true}
            resolution={resolution}
            mousePos={mousePos}
            postfx={{
              scanlineIntensity: 0.2,
              scanlineCount: 150,
              targetFPS: 0,
              jitterIntensity: 0,
              jitterSpeed: 1,
              mouseGlowEnabled: false,
              mouseGlowRadius: 200,
              mouseGlowIntensity: 1.5,
              vignetteIntensity: 0.6,
              vignetteRadius: 1.2,
              colorPalette: 0,
              curvature: 0,
              aberrationStrength: 0,
              noiseIntensity: 0.02,
              noiseScale: 1,
              noiseSpeed: 0.5,
              waveAmplitude: 0,
              waveFrequency: 10,
              waveSpeed: 1,
              glitchIntensity: 0,
              glitchFrequency: 0,
              brightnessAdjust: 0,
              contrastAdjust: 1.1,
            }}
          />
        </EffectComposer>
      </Canvas>
    </div>
  )
}
