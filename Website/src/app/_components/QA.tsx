import { ReactNode, useEffect, useState } from "react";

interface QAProps {
  question: string;
  answer: ReactNode;
  calcButtonPosition: () => void;
}

function QA({ question, answer, calcButtonPosition }: QAProps) {
  const [showAnswer, setShowAnswer] = useState(false);

  const toggleQA = () => {
    setShowAnswer((prev) => !prev);
  };

  useEffect(() => {
    calcButtonPosition();
  }, [showAnswer]);

  return (
    <article className="cursor-pointer" onClick={toggleQA}>
      <button
        className={`px-5 py-7 transition-colors duration-500 ${
          !showAnswer && "hover:bg-[#f6f6f6]"
        }`}
      >
        <div className="flex items-center gap-2">
          <span className="text-xs text-gray-300 font-bold">Q.</span>
          <p className="text-gray-700 text-[15px]">{question}</p>
        </div>
      </button>

      {showAnswer && (
        <button
          className="text-left bg-[#f7f7f7] p-5 flex gap-2 text-gray-600"
          onLoad={calcButtonPosition}
        >
          <span className="text-xs text-gray-300 font-bold">A.</span>
          {answer}
        </button>
      )}
    </article>
  );
}

export default QA;
