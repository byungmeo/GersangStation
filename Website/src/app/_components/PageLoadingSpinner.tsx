import LoadingSpinner from "@/_components/LoadingSpinner";

interface PageLoadingSpinnerProps {
  text: string;
}

function PageLoadingSpinner({ text }: PageLoadingSpinnerProps) {
  return (
    <div className="h-[100dvh] flex flex-col justify-center items-center">
      <LoadingSpinner />
      {text}
    </div>
  );
}

export default PageLoadingSpinner;
