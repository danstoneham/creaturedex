import dynamic from "next/dynamic";

const AnimalHabitatMap = dynamic(
  () => import("./AnimalHabitatMap").then((m) => m.AnimalHabitatMap),
  { ssr: false }
);

export { AnimalHabitatMap };
