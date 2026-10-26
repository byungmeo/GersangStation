import { ReactNode, useCallback, useEffect, useRef, useState } from "react";

import Markdown from "react-markdown";
// remark and rehype plugins
import rehypeRaw from "rehype-raw";
import remarkMath from "remark-math";
import rehypeKatex from "rehype-katex";
import remarkGfm from "remark-gfm";

import QA from "@/_components/QA";
import Modal from "@/_components/Modal";
import FallbackImage from "@/_components/FallbackImage";

import "github-markdown-css/github-markdown-light.css";

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
    filename: "1.다클복붙문제.md",
    question: "\"클라이언트 버전이 서버와 틀립니다.\"",
  },
  {
    filename: "2.소켓문제.md",
    question: "\"엑세스 권한에 의해 숨겨진 소켓에 엑세스를 시도했습니다.\"",
  },
  {
    filename: "3.바로가기문제.md",
    question: "\"지정된 모듈을 찾을 수 없습니다.\"",
  },
  {
    filename: "4.IP차단문제.md",
    question: "계정 정지 또는 \"해당IP는 로그인이 차단되었습니다.\"",
  },
  {
    filename: "5.무반응문제.md",
    question: "\"프로그램을 실행해도 아무 반응이 없습니다.\"",
  }
];

function Page() {
  const [modalOpen, setModalOpen] = useState(false);
  const pannerRef = useRef<HTMLDivElement>(null);
  const buttonRef = useRef<HTMLDivElement>(null);
  const qaContainerRef = useRef<HTMLDivElement>(null);
  const buttonPlaceholderRef = useRef<HTMLDivElement>(null);

  const [QAs, setQAs] = useState<QAInfo[]>([]);
  const scrollingDown = useRef(false);
  const prevScrollY = useRef(scrollY);

  const calcButtonPosition = useCallback(() => {
    if (prevScrollY.current != scrollY)
      scrollingDown.current = prevScrollY.current < scrollY;

    const footerY = document.body.scrollHeight - 250;
    const scrollBottom = window.scrollY + window.innerHeight;
    const diff = scrollBottom - footerY;

    if (diff > 0) {
      buttonPlaceholderRef.current!.style.display = "none";
      buttonRef.current!.style.position = "static";
    } else {
      buttonPlaceholderRef.current!.style.display = "block";
      buttonRef.current!.style.position = "fixed";
    }

    prevScrollY.current = scrollY;
  }, []);

  function resizeContent() {
    buttonRef.current!.style.width = `${pannerRef.current!.clientWidth}px`;
    qaContainerRef.current!.style.width = `${pannerRef.current!.clientWidth}px`;
  }

  useEffect(() => {
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
              answer: (
                <Markdown
                  skipHtml={false}
                  remarkPlugins={[remarkMath, remarkGfm]}
                  rehypePlugins={[rehypeRaw, rehypeKatex]}
                  components={{
                    a: ({ ...props }) => <a {...props} target="_blank" />,
                    img: ({ ...props }) => (
                      <FallbackImage
                        src={props.src!}
                      />
                    ),
                  }}
                  className="inline"
                >
                  {text}
                </Markdown>
              ),
            },
          ]);
          fetchQA(index + 1);
        })
        .catch((err) => {
          console.error(err);
        });
    }
    fetchQA(0);
    resizeContent();

    document.addEventListener("scroll", calcButtonPosition);
    window.addEventListener("resize", calcButtonPosition);
    document.addEventListener("orientationchange", calcButtonPosition);
    window.addEventListener("resize", resizeContent);

    return () => {
      document.removeEventListener("scroll", calcButtonPosition);
      window.removeEventListener("resize", calcButtonPosition);
      document.removeEventListener("orientationchange", calcButtonPosition);
      window.removeEventListener("resize", resizeContent);
    };
  }, [calcButtonPosition]);

  return (
    <>
      <div
        ref={pannerRef}
        className="flex flex-col h-full min-h-[100vh] w-full"
      >
        <div className="absolute w-full flex justify-center">
          <div className="w-[20vw] min-w-[120px] max-w-[170px] h-[6px] bg-gray-400/90 rounded-full -translate-y-[1.5px] lg:hidden" />
        </div>

        <h1
          ref={qaContainerRef}
          className="lg:fixed border-b-[1px] border-b-gray-200 bg-transparent lg:bg-indigo-50"
        >
          <div
            className="flex gap-3 items-center text-gray-800 font-bold 
            py-4 lg:py-2 pl-4"
          >
            <p className="text-2xl animate-wobble">📢</p>
            <p className="lg:font-[Dongle] text-[19px] lg:text-[40px] lg:text-indigo-600 text-gray-600">
              자주 묻는 질문
            </p>
          </div>
        </h1>
        <div className="hidden lg:block h-[72.67px]" />

        {/* 자주 묻는 질문 */}
        <section className="w-full h-full flex flex-col">
          {QAs.map((qa, index) => (
            <QA
              key={index}
              question={qa.question}
              answer={qa.answer}
              calcButtonPosition={calcButtonPosition}
            />
          ))}
        </section>

        <div
          ref={buttonPlaceholderRef}
          className="block h-[65.33px] lg:h-[81.33px] w-full"
        />

        <div
          ref={buttonRef}
          className="block mt-auto bottom-0 px-3 py-3 
          bg-white border-t-[1px] border-gray-200 rounded-b-2xl lg:rounded-none lg:animate-show-up"
        >
          <button
            onClick={() => setModalOpen(true)}
            className="block mx-auto max-w-[500px] h-full w-full rounded-full p-2 lg:p-4 bg-indigo-600 text-white font-semibold
            transition-transform hover:scale-[103%] duration-500 text-center"
          >
            1:1 문의하기
          </button>
        </div>
      </div>

      <Modal open={modalOpen} onClose={() => setModalOpen(false)} />
    </>
  );
}

export default Page;
