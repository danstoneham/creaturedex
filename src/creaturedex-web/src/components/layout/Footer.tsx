import Link from "next/link";

export default function Footer() {
  return (
    <footer className="bg-primary-dark text-white mt-auto">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <div className="grid grid-cols-1 md:grid-cols-3 gap-8">
          <div>
            <h3 className="text-lg font-bold mb-3">🐾 Creaturedex</h3>
            <p className="text-sm text-gray-300">
              An AI-powered animal encyclopedia. Discover, learn, and find your
              perfect pet companion.
            </p>
          </div>
          <div>
            <h4 className="font-semibold mb-3">Explore</h4>
            <ul className="space-y-2 text-sm text-gray-300">
              <li><Link href="/animals" className="hover:text-white transition-colors">Browse Animals</Link></li>
              <li><Link href="/matcher" className="hover:text-white transition-colors">Pet Matcher</Link></li>
              <li><Link href="/search" className="hover:text-white transition-colors">Search</Link></li>
            </ul>
          </div>
          <div>
            <h4 className="font-semibold mb-3">About</h4>
            <p className="text-sm text-gray-300">
              Content is AI-generated and should be verified with professional
              sources for medical or care decisions.
            </p>
          </div>
        </div>
        <div className="border-t border-gray-600 mt-8 pt-4 text-center text-xs text-gray-400">
          &copy; {new Date().getFullYear()} Creaturedex. Built with AI.
        </div>
      </div>
    </footer>
  );
}
