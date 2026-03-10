"use client";

import { MapContainer, TileLayer } from "react-leaflet";
import "leaflet/dist/leaflet.css";

interface AnimalHabitatMapProps {
  tileUrlTemplate: string;
  centerLat: number;
  centerLng: number;
  zoom: number;
  observationCount?: number;
}

export function AnimalHabitatMap({
  tileUrlTemplate,
  centerLat,
  centerLng,
  zoom,
  observationCount,
}: AnimalHabitatMapProps) {
  return (
    <div className="relative rounded-xl overflow-hidden border border-[#D4C4B0]">
      <MapContainer
        center={[centerLat, centerLng]}
        zoom={zoom}
        scrollWheelZoom={false}
        style={{ height: "400px", width: "100%", background: "#FAFAF8" }}
      >
        <TileLayer
          url="https://tile.gbif.org/3857/omt/{z}/{x}/{y}@1x.png?style=gbif-light"
          attribution='&copy; <a href="https://www.gbif.org">GBIF</a>'
        />
        <TileLayer
          url={tileUrlTemplate}
          attribution='&copy; <a href="https://www.gbif.org">GBIF</a>'
          opacity={0.85}
        />
      </MapContainer>
      {observationCount != null && observationCount > 0 && (
        <div className="absolute top-3 right-3 z-[1000] bg-white/90 border border-[#D4C4B0] rounded-lg px-3 py-1.5 text-sm shadow-sm">
          <span className="font-semibold text-[#2D6A4F]">
            {observationCount.toLocaleString()}
          </span>{" "}
          <span className="text-[#6B5A4E]">observations</span>
        </div>
      )}
    </div>
  );
}
