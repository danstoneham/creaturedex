"use client";

interface EditToolbarProps {
  isEditing: boolean;
  isPublished: boolean;
  isSaving: boolean;
  isGeneratingImage: boolean;
  isReviewing: boolean;
  isFetchingWikiImage?: boolean;
  isRegenerating?: boolean;
  onToggleEdit: () => void;
  onSave: () => void;
  onCancel: () => void;
  onGenerateImage: () => void;
  onUploadImage: () => void;
  onFetchWikipediaImage?: () => void;
  onRegenerate?: () => void;
  onReview: () => void;
  onTogglePublish: () => void;
}

export default function EditToolbar({
  isEditing, isPublished, isSaving, isGeneratingImage, isReviewing,
  isFetchingWikiImage, isRegenerating,
  onToggleEdit, onSave, onCancel, onGenerateImage, onUploadImage,
  onFetchWikipediaImage, onRegenerate,
  onReview, onTogglePublish,
}: EditToolbarProps) {
  return (
    <div className="bg-surface border border-[#3D2A1D] rounded-xl p-3 mb-6 flex flex-wrap items-center gap-2">
      {isEditing ? (
        <>
          <button
            onClick={onSave}
            disabled={isSaving}
            className="bg-primary text-white px-4 py-1.5 rounded-lg text-sm font-medium hover:bg-primary/90 disabled:opacity-50"
          >
            {isSaving ? "Saving..." : "Save"}
          </button>
          <button
            onClick={onCancel}
            disabled={isSaving}
            className="bg-[#3D2A1D] text-[#C4B5A4] px-4 py-1.5 rounded-lg text-sm hover:bg-[#4A3728]"
          >
            Cancel
          </button>
          <div className="w-px h-6 bg-[#3D2A1D] mx-1" />
        </>
      ) : (
        <button
          onClick={onToggleEdit}
          className="bg-primary/10 text-primary px-4 py-1.5 rounded-lg text-sm font-medium hover:bg-primary/20"
        >
          Edit
        </button>
      )}

      <button
        onClick={onGenerateImage}
        disabled={isGeneratingImage}
        className="bg-purple-900/40 text-purple-300 px-4 py-1.5 rounded-lg text-sm font-medium hover:bg-purple-900/60 disabled:opacity-50"
      >
        {isGeneratingImage ? "Generating..." : "Generate Image"}
      </button>

      <button
        onClick={onUploadImage}
        className="bg-[#3D2A1D] text-[#C4B5A4] px-4 py-1.5 rounded-lg text-sm hover:bg-[#4A3728]"
      >
        Upload Image
      </button>

      {onFetchWikipediaImage && (
        <button
          onClick={onFetchWikipediaImage}
          disabled={isFetchingWikiImage}
          className="bg-emerald-900/40 text-emerald-300 px-4 py-1.5 rounded-lg text-sm font-medium hover:bg-emerald-900/60 disabled:opacity-50"
        >
          {isFetchingWikiImage ? "Fetching..." : "Wiki Image"}
        </button>
      )}

      <button
        onClick={onReview}
        disabled={isReviewing}
        className="bg-blue-900/40 text-blue-300 px-4 py-1.5 rounded-lg text-sm font-medium hover:bg-blue-900/60 disabled:opacity-50"
      >
        {isReviewing ? "Reviewing..." : "AI Review"}
      </button>

      <div className="ml-auto flex items-center gap-2">
        {onRegenerate && (
          <button
            onClick={onRegenerate}
            disabled={isRegenerating}
            className="bg-orange-900/40 text-orange-300 px-4 py-1.5 rounded-lg text-sm font-medium hover:bg-orange-900/60 disabled:opacity-50"
          >
            {isRegenerating ? "Regenerating..." : "Regenerate"}
          </button>
        )}
        <button
          onClick={onTogglePublish}
          className={`px-4 py-1.5 rounded-lg text-sm font-medium ${
            isPublished
              ? "bg-red-900/40 text-red-300 hover:bg-red-900/60"
              : "bg-green-900/40 text-green-300 hover:bg-green-900/60"
          }`}
        >
          {isPublished ? "Unpublish" : "Publish"}
        </button>
      </div>
    </div>
  );
}
