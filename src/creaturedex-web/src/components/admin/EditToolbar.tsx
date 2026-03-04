"use client";

interface EditToolbarProps {
  isEditing: boolean;
  isPublished: boolean;
  isSaving: boolean;
  isGeneratingImage: boolean;
  isReviewing: boolean;
  onToggleEdit: () => void;
  onSave: () => void;
  onCancel: () => void;
  onGenerateImage: () => void;
  onUploadImage: () => void;
  onReview: () => void;
  onTogglePublish: () => void;
}

export default function EditToolbar({
  isEditing, isPublished, isSaving, isGeneratingImage, isReviewing,
  onToggleEdit, onSave, onCancel, onGenerateImage, onUploadImage,
  onReview, onTogglePublish,
}: EditToolbarProps) {
  return (
    <div className="bg-surface border border-gray-200 rounded-xl p-3 mb-6 flex flex-wrap items-center gap-2">
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
            className="bg-gray-100 text-text px-4 py-1.5 rounded-lg text-sm hover:bg-gray-200"
          >
            Cancel
          </button>
          <div className="w-px h-6 bg-gray-200 mx-1" />
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
        className="bg-secondary/10 text-secondary px-4 py-1.5 rounded-lg text-sm font-medium hover:bg-secondary/20 disabled:opacity-50"
      >
        {isGeneratingImage ? "Generating..." : "Generate Image"}
      </button>

      <button
        onClick={onUploadImage}
        className="bg-gray-100 text-text px-4 py-1.5 rounded-lg text-sm hover:bg-gray-200"
      >
        Upload Image
      </button>

      <button
        onClick={onReview}
        disabled={isReviewing}
        className="bg-blue-50 text-blue-700 px-4 py-1.5 rounded-lg text-sm font-medium hover:bg-blue-100 disabled:opacity-50"
      >
        {isReviewing ? "Reviewing..." : "AI Review"}
      </button>

      <div className="ml-auto">
        <button
          onClick={onTogglePublish}
          className={`px-4 py-1.5 rounded-lg text-sm font-medium ${
            isPublished
              ? "bg-red-50 text-red-700 hover:bg-red-100"
              : "bg-green-50 text-green-700 hover:bg-green-100"
          }`}
        >
          {isPublished ? "Unpublish" : "Publish"}
        </button>
      </div>
    </div>
  );
}
