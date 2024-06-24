import QA from "@/_components/QA";
import { ReactNode, useEffect, useRef, useState } from "react";
import Modal from "@/_components/Modal";
import Markdown from "react-markdown";

interface QAInfo {
  question: string;
  answer: ReactNode;
}

/*
  QA 작성 방법:
  1. /public/qa 폴더에 md 파일을 작성한다.
  2. QAList에 작성한 파일명과 질문이름을 추가한다.
  - 파일명은 확장자를 포함한다.
  - QAList에 추가된 순서대로 질문이 출력된다.
  - 파일이름에 물음표같은 특수문자가 들어갈 수 없어서 아래 배열에 질문을 추가할 때 
  질문이름과 파일이름을 따로 작성하도록 했다.
  - 이미지를 넣고 싶다면 /public/images 폴더에 이미지를 넣고 md 파일에 이미지 경로를 작성한다.
  이미지 경로는 /GersantStation/images/이미지파일명.jpg 로 작성한다.
  예시로 예시파일.md을 참고
*/
const QAList: {
  filename: string;
  question: string;
}[] = [
  {
    filename: "예시파일.md",
    question: "거상이란 무엇인가요?",
  },
];

function Page() {
  const [modalOpen, setModalOpen] = useState(false);
  const buttonRef = useRef<HTMLDivElement>(null);
  const [QAs, setQAs] = useState<QAInfo[]>([]);

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

  function fetchQA(index: number) {
    if (index >= QAList.length) return;
    fetch(
      `/${import.meta.env.VITE_REPOSITORY_NAME}/answers/${
        QAList[index].filename
      }`
    )
      .then((res) => {
        if (res.ok) return res.text();
        else throw new Error("Failed to fetch");
      })
      .then((text) => {
        setQAs((prev) => [
          ...prev,
          {
            question: QAList[index].question,
            answer: <Markdown className="inline">{text}</Markdown>,
          },
        ]);
        fetchQA(index + 1);
      })
      .catch((err) => {
        console.error(err);
      });
  }

  useEffect(() => {
    fetchQA(0);

    calcHeaderPosition();
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

        <div
          className="lg:fixed w-full lg:max-w-[460px] xl:max-w-[560px] flex py-3 gap-3 items-center px-4 text-gray-800 font-bold border-b-[1px] bg-white
          border-x-[1px] border-gray-200"
        >
          <p className="text-2xl animate-wobble">📢</p>
          <p className="font-[Dongle] text-[40px] text-indigo-600 ">
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
        className="fixed px-3 w-full max-w-[920px] lg:max-w-[460px] xl:max-w-[560px] py-3 bg-white lg:bg-transparent border-t-[1px] border-gray-300"
        style={{ bottom: "-100vh" }}
      >
        <button
          onClick={() => setModalOpen(true)}
          className="block h-full w-full rounded-full p-2 lg:p-4 bg-indigo-600 text-white font-semibold
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
