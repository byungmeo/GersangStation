import { useEffect, useRef, useState } from "react";

interface ModalProps {
  open: boolean;
  onClose: () => void;
}

function Modal({ open, onClose }: ModalProps) {
  const modalContainer = useRef<HTMLDivElement>(null);
  const [justMounded, setJustMounted] = useState(true);

  useEffect(() => {
    setJustMounted(false);
  }, []);
  function handleClose() {
    onClose();
  }

  if (justMounded) return null;

  return (
    <div
      ref={modalContainer}
      className={`fixed h-[100dvh] w-full top-0 left-0 flex justify-center items-center
    bg-white bg-opacity-50 transition-opacity duration-500 ${
      open ? "opacity-100" : "opacity-0 pointer-events-none"
    }`}
    >
      <div
        className="flex flex-col pt-2 px-6 pb-6 rounded-2xl h-fit max-w-[500px] w-full items-center mx-4 lg:mx-0
      bg-white shadow-lg border-2 border-[#f1f1f1]"
      >
        <button
          className="ml-auto text-2xl font-semibold"
          onClick={handleClose}
        >
          x
        </button>
        <p className="text-xl font-bold mb-3">문의 전 참고사항</p>
        <ol className="list-decimal">
          <li className="">
            <p>문의 전 거상 점검 시간인지 확인 해주세요. (일부 기능 제한)</p>
          </li>
          <li className="">
            <p>오류가 발생하는 경우 반드시 스크린샷을 함께 준비 해주세요.</p>
          </li>
          <li className="">
            <p>답변이 늦을 수 있습니다. 미리 문의내용 적어주시고 기다려주세요.</p>
          </li>
        </ol>
        <a
          href="https://open.kakao.com/o/sXJQ1qPd"
          target="_blank"
          className="block mt-7 bg-[#6151f3] px-4 py-2 lg:py-4 lg:px-7 rounded-full text-white font-semibold text-sm
            transition-transform hover:scale-105 duration-500"
        >
          네, 확인 했습니다.
        </a>
      </div>
    </div>
  );
}

export default Modal;
