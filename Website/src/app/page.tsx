import QA from "@/_components/QA";
import { ReactNode, useState } from "react";
import SampleImage from "@/_assets/images/샘플사진.jpg";
import Modal from "./_components/Modal";

const QAs: { question: string; answer: ReactNode }[] = [
  {
    question: "거렌더는 어떻게 사용하나요?",
    answer: (
      <>
        <p className="inline">답변할때는 이렇게 하면 됩니다</p>
        <div className="h-4" />
        <p>
          inline클래스는 줄바꿈을 안 시키고, block 클래스는 줄바꿈 해주는 거라고
          보면 됨.
        </p>
        <img src={SampleImage} alt="샘플 사진" className="w-full my-1" />
        <strong className="block">나는 스트롱</strong>
        <i className="block">나는 이탤릭</i>
        <p>태그 간격 사이는 my-1 my-2 my-3 이나 mt-1 mt-2 mb-1 mb-2</p>
      </>
    ),
  },
  {
    question: "거렌더는 어떻게 사용하나요?",
    answer: (
      <>
        <p className="inline">답변할때는 이렇게 하면 됩니다</p>
        <div className="h-4" />
        <p>
          inline클래스는 줄바꿈을 안 시키고, block 클래스는 줄바꿈 해주는 거라고
          보면 됨.
        </p>
        <img src={SampleImage} alt="샘플 사진" className="w-full my-1" />
        <strong className="block">나는 스트롱</strong>
        <i className="block">나는 이탤릭</i>
        <p>태그 간격 사이는 my-1 my-2 my-3 이나 mt-1 mt-2 mb-1 mb-2</p>
      </>
    ),
  },
  {
    question: "거렌더는 어떻게 사용하나요?",
    answer: (
      <>
        <p className="inline">답변할때는 이렇게 하면 됩니다</p>
        <div className="h-4" />
        <p>
          inline클래스는 줄바꿈을 안 시키고, block 클래스는 줄바꿈 해주는 거라고
          보면 됨.
        </p>
        <img src={SampleImage} alt="샘플 사진" className="w-full my-1" />
        <strong className="block">나는 스트롱</strong>
        <i className="block">나는 이탤릭</i>
        <p>태그 간격 사이는 my-1 my-2 my-3 이나 mt-1 mt-2 mb-1 mb-2</p>
      </>
    ),
  },
  {
    question: "거렌더는 어떻게 사용하나요?",
    answer: (
      <>
        <p className="inline">답변할때는 이렇게 하면 됩니다</p>
        <div className="h-4" />
        <p>
          inline클래스는 줄바꿈을 안 시키고, block 클래스는 줄바꿈 해주는 거라고
          보면 됨.
        </p>
        <img src={SampleImage} alt="샘플 사진" className="w-full my-1" />
        <strong className="block">나는 스트롱</strong>
        <i className="block">나는 이탤릭</i>
        <p>태그 간격 사이는 my-1 my-2 my-3 이나 mt-1 mt-2 mb-1 mb-2</p>
      </>
    ),
  },
  {
    question: "거렌더는 어떻게 사용하나요?",
    answer: (
      <>
        <p className="inline">답변할때는 이렇게 하면 됩니다</p>
        <div className="h-4" />
        <p>
          inline클래스는 줄바꿈을 안 시키고, block 클래스는 줄바꿈 해주는 거라고
          보면 됨.
        </p>
        <img src={SampleImage} alt="샘플 사진" className="w-full my-1" />
        <strong className="block">나는 스트롱</strong>
        <i className="block">나는 이탤릭</i>
        <p>태그 간격 사이는 my-1 my-2 my-3 이나 mt-1 mt-2 mb-1 mb-2</p>
      </>
    ),
  },
];

function Page() {
  const [modalOpen, setModalOpen] = useState(false);

  return (
    <>
      <div className="flex flex-col h-full overflow-hidden">
        <div
          className="mx-auto w-[20vw] min-w-[120px] max-w-[170px] h-[6px] bg-gray-400/90 rounded-full -translate-y-[1.5px]
        lg:hidden"
        />

        <p className="pt-6 pb-4 px-4 text-gray-800 text-2xl font-bold border-b-[1px]">
          📢 자주 묻는 질문
        </p>

        {/* 자주 묻는 질문 */}
        <div className="flex flex-col">
          {QAs.map((qa, index) => (
            <QA key={index} question={qa.question} answer={qa.answer} />
          ))}
        </div>

        <div className="h-[80px]" />
        <div className="fixed px-4 bottom-0 w-full max-w-[920px] lg:max-w-[460px] py-4 bg-white">
          <button
            onClick={() => setModalOpen(true)}
            className="block h-full w-full rounded-full p-2 lg:p-4 bg-[#6151f3] text-white font-semibold
            transition-transform hover:scale-105 duration-500 text-center"
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
