import Link from "next/link";

export default function Footer() {
  return (
    <footer className="bg-surface text-white mt-auto">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <div className="grid grid-cols-1 md:grid-cols-3 gap-8">
          <div>
            {/* eslint-disable-next-line @next/next/no-img-element */}
            <img
              src="/images/logo-compact.png"
              alt="Creaturedex"
              className="h-14 w-auto mb-3"
            />
            <p className="text-sm text-[#C4B5A4]">
              An AI-powered animal encyclopedia. Discover, learn, and find your
              perfect pet companion.
            </p>
          </div>
          <div>
            <h4 className="font-semibold mb-3">Explore</h4>
            <ul className="space-y-2 text-sm text-[#C4B5A4]">
              <li><Link href="/animals" className="hover:text-white transition-colors">Browse Animals</Link></li>
              <li><Link href="/matcher" className="hover:text-white transition-colors">Pet Matcher</Link></li>
              <li><Link href="/search" className="hover:text-white transition-colors">Search</Link></li>
            </ul>
          </div>
          <div>
            <h4 className="font-semibold mb-3">About</h4>
            <p className="text-sm text-[#C4B5A4]">
              Content is AI-generated and should be verified with professional
              sources for medical or care decisions.
            </p>
          </div>
        </div>
        <div className="border-t border-[#3D2A1D] mt-8 pt-4 text-center text-xs text-[#8B7355]">
          <p>&copy; {new Date().getFullYear()} Creaturedex. Built with AI.</p>
          <p className="mt-1">
            Occurrence data from{" "}
            <a href="https://www.gbif.org" target="_blank" rel="noopener noreferrer" className="underline hover:text-[#C4B5A4]">GBIF.org</a>
            {" "}&middot;{" "}
            Taxonomy from{" "}
            <a href="https://www.catalogueoflife.org" target="_blank" rel="noopener noreferrer" className="underline hover:text-[#C4B5A4]">Catalogue of Life</a>
          </p>
        </div>
      </div>
    </footer>
  );
}
