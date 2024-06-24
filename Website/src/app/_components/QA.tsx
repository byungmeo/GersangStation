import { ReactNode, useEffect, useState } from "react";

interface QAProps {
  question: string;
  answer: ReactNode;
  calcHeaderPosition: (button: React.RefObject<HTMLDivElement>) => void;
  buttonRef: React.RefObject<HTMLDivElement>;
}

function QA({ question, answer, calcHeaderPosition, buttonRef }: QAProps) {
  const [showAnswer, setShowAnswer] = useState(false);

  const toggleQA = () => {
    setShowAnswer((prev) => !prev);
  };

  useEffect(() => {
    calcHeaderPosition(buttonRef);
  }, [showAnswer]);

  return (
    <button onClick={toggleQA}>
      <div
        className={`px-5 py-7 transition-colors duration-500 ${
          !showAnswer && "hover:bg-[#f6f6f6]"
        }`}
      >
        <div className="flex items-center gap-2">
          <span className="text-xs text-gray-300 font-bold">Q.</span>
          <p className="text-gray-700 text-[15px]">{question}</p>
        </div>
      </div>

      {showAnswer && (
        <div className="text-left bg-[#f7f7f7] p-5 flex gap-2 text-gray-600">
          <span className="text-xs text-gray-300 font-bold">A.</span>
          {answer}
        </div>
      )}
    </button>
  );
}

export default QA;
