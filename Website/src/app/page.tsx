import QA from "@/_components/QA";
import { ReactNode, useEffect, useRef, useState } from "react";
// import SampleImage from "@/_assets/images/샘플사진.jpg";
import Modal from "./_components/Modal";

const QAs: { question: string; answer: ReactNode }[] = [
  {
    question: "준비 중 1",
    answer: (
      <>
        <p className="inline">준비 중입니다.</p>
        <div className="h-4" />
        <p>"inline" - 줄바꿈 없음 "block" - 줄바꿈</p>
        <img
          src="https://placehold.co/300x200/png"
          alt="샘플 사진"
          className="w-full my-1"
        />
        <strong className="block">strong</strong>
        <i className="block">italic</i>
        <p>태그 간격 my-1 my-2 my-3 또는 mt-1 mt-2 mb-1 mb-2</p>
      </>
    ),
  },
  {
    question: "준비 중 2",
    answer: (
      <>
        <p className="inline">준비 중입니다.</p>
        <div className="h-4" />
        <p>"inline" - 줄바꿈 없음 "block" - 줄바꿈</p>
        <img
          src="https://placehold.co/300x200/png"
          alt="샘플 사진"
          className="w-full my-1"
        />
        <strong className="block">strong</strong>
        <i className="block">italic</i>
        <p>태그 간격 my-1 my-2 my-3 또는 mt-1 mt-2 mb-1 mb-2</p>
      </>
    ),
  },
  {
    question: "준비 중 3",
    answer: (
      <>
        <p className="inline">준비 중입니다.</p>
        <div className="h-4" />
        <p>"inline" - 줄바꿈 없음 "block" - 줄바꿈</p>
        <img
          src="https://placehold.co/300x200/png"
          alt="샘플 사진"
          className="w-full my-1"
        />
        <strong className="block">strong</strong>
        <i className="block">italic</i>
        <p>태그 간격 my-1 my-2 my-3 또는 mt-1 mt-2 mb-1 mb-2</p>
      </>
    ),
  },
  {
    question: "준비 중 4",
    answer: (
      <>
        <p className="inline">준비 중입니다.</p>
        <div className="h-4" />
        <p>"inline" - 줄바꿈 없음 "block" - 줄바꿈</p>
        <img
          src="https://placehold.co/300x200/png​"
          alt="샘플 사진"
          className="w-full my-1"
        />
        <strong className="block">strong</strong>
        <i className="block">italic</i>
        <p>태그 간격 my-1 my-2 my-3 또는 mt-1 mt-2 mb-1 mb-2</p>
      </>
    ),
  },
  {
    question: "준비 중 5",
    answer: (
      <>
        <p className="inline">준비 중입니다.</p>
        <div className="h-4" />
        <p>"inline" - 줄바꿈 없음 "block" - 줄바꿈</p>
        <img
          src="https://placehold.co/300x200/png"
          alt="샘플 사진"
          className="w-full py-1"
        />
        <strong className="block">strong</strong>
        <i className="block">italic</i>
        <p>태그 간격 my-1 my-2 my-3 또는 mt-1 mt-2 mb-1 mb-2</p>
      </>
    ),
  },
];

function Page() {
  const [modalOpen, setModalOpen] = useState(false);
  const buttonRef = useRef<HTMLDivElement>(null);

  function calcHeaderPosition() {
    const footer = document.querySelector("footer");
    const footerY = document.body.scrollHeight - footer!.clientHeight;
    const scrollBottom = window.scrollY + window.innerHeight;
    const diff = scrollBottom - footerY;

    if (diff > 0) {
      buttonRef.current!.style.bottom = `${diff}px`;
    } else {
      buttonRef.current!.style.bottom = `0px`;
    }
  }

  useEffect(() => {
    document.addEventListener("scroll", calcHeaderPosition);
    document.addEventListener("click", calcHeaderPosition);
    document.addEventListener("touchend", calcHeaderPosition);
    document.addEventListener("resize", calcHeaderPosition);
    document.addEventListener("orientationchange", calcHeaderPosition);

    return () => {
      document.removeEventListener("scroll", calcHeaderPosition);
      document.removeEventListener("click", calcHeaderPosition);
      document.removeEventListener("touchend", calcHeaderPosition);
      document.removeEventListener("resize", calcHeaderPosition);
      document.removeEventListener("orientationchange", calcHeaderPosition);
    };
  }, []);
  
  return (
    <>
      <div className="flex flex-col h-full overflow-hidden">
        <div
          className="mx-auto w-[20vw] min-w-[120px] max-w-[170px] h-[6px] bg-gray-400/90 rounded-full -translate-y-[1.5px]
          lg:hidden"
        />

        <div className="flex pt-6 pb-3 gap-3 items-center px-4 text-gray-800 font-bold border-b-[1px] bg-white">
          <p className="text-2xl animate-wobble">📢</p>
          <p className="font-[Dongle] text-[40px] text-pink-400 ">
            자주 묻는 질문
          </p>
        </div>

        {/* 자주 묻는 질문 */}
        <div className="flex flex-col">
          {QAs.map((qa, index) => (
            <QA key={index} question={qa.question} answer={qa.answer} />
          ))}
        </div>

        <div className="h-[80px]" />
      </div>
      <div
        ref={buttonRef}
        className="fixed px-4 bottom-0 w-full max-w-[920px] lg:max-w-[460px] xl:max-w-[560px] py-4"
      >
        <button
          onClick={() => setModalOpen(true)}
          className="block h-full w-full rounded-full p-2 lg:p-4 bg-pink-400 text-white font-semibold
            transition-transform hover:scale-[103%] duration-500 text-center"
        >
          1:1 문의하기
        </button>
      </div>

      <Modal open={modalOpen} onClose={() => setModalOpen(false)} />
    </>
  );
}

export default Page;
