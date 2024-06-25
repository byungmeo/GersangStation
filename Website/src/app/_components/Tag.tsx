interface TagProps {
  text: string;
}

function Tag({ text }: TagProps) {
  return (
    <div
      className="bg-white lg:bg-transparent border-[1px] border-indigo-600 rounded-full lg:px-4 lg:py-[6px] pointer-events-none z-[-1]
      text-indigo-600 text-xs lg:text-basefont-semibold px-3 py-[5px] font-semibold"
    >
      {text}
    </div>
  );
}

export default Tag;
