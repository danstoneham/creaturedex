interface ImageAttributionProps {
  license?: string;
  rightsHolder?: string;
  source?: string;
}

export default function ImageAttribution({
  license,
  rightsHolder,
  source,
}: ImageAttributionProps) {
  if (!license && !rightsHolder && !source) return null;

  return (
    <p className="text-xs text-text-muted mt-1.5 leading-relaxed">
      {rightsHolder && (
        <span>&copy; {rightsHolder}</span>
      )}
      {rightsHolder && license && <span> &middot; </span>}
      {license && <span>{license}</span>}
      {(rightsHolder || license) && source && <span> &middot; </span>}
      {source && (
        <span>
          Source:{" "}
          {source.startsWith("http") ? (
            <a
              href={source}
              target="_blank"
              rel="noopener noreferrer"
              className="underline hover:text-primary"
            >
              link
            </a>
          ) : (
            source
          )}
        </span>
      )}
    </p>
  );
}
