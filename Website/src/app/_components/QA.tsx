import { ReactNode, useState } from "react";

interface QAProps {
  question: string;
  answer: ReactNode;
}

function QA({ question, answer }: QAProps) {
  const [showAnswer, setShowAnswer] = useState(false);

  const toggleQA = () => {
    setShowAnswer((prev) => !prev);
  };

  return (
    <button onClick={toggleQA}>
      <div
        className={`px-5 py-7 transition-colors duration-500 ${
          !showAnswer && "hover:bg-[#f6f6f6]"
        }`}
      >
        <div className="flex items-center">
          <span className="text-xs text-gray-300 font-bold">
            Q.&nbsp;&nbsp;
          </span>
          <p className="text-gray-700 text-[15px]">{question}</p>
        </div>
      </div>

      {showAnswer && (
        <div className="text-left bg-[#fbfbfb] p-5 items-center">
          <span className="text-xs text-gray-300 font-bold">
            A.&nbsp;&nbsp;
          </span>
          {answer}
        </div>
      )}
    </button>
  );
}

export default QA;
