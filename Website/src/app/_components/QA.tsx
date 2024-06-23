import { ReactNode, useRef, useState } from "react";

interface QAProps {
  question: string;
  answer: ReactNode;
}

function QA({ question, answer }: QAProps) {
  const [showAnswer, setShowAnswer] = useState(false);
  const answerContainerRef = useRef<HTMLDivElement>(null);

  const toggleQA = () => {
    setShowAnswer((prev) => !prev);
    setTimeout(
      () => {
        answerContainerRef.current!.style.position = showAnswer
          ? "absolute"
          : "relative";
        answerContainerRef.current!.style.visibility = showAnswer
          ? "hidden"
          : "visible";
        answerContainerRef.current!.style.display = showAnswer
          ? "none"
          : "block";
      },
      showAnswer ? 300 : 0
    );
  };

  return (
    <button onClick={toggleQA}>
      <div
        className={`p-5 transition-colors duration-500 ${
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

      <div
        ref={answerContainerRef}
        className={`text-left bg-[#f2f4f6] p-5 items-center transition-opacity duration-300
        ${showAnswer ? "opacity-100" : "opacity-0"}`}
        style={{
          position: "absolute",
          visibility: "hidden",
        }}
      >
        <span className="text-xs text-gray-300 font-bold">A.&nbsp;&nbsp;</span>
        {answer}
      </div>
    </button>
  );
}

export default QA;
