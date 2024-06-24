interface TagProps {
  text: string;
}

function Tag({ text }: TagProps) {
  return (
    <div
      className="bg-white lg:bg-transparent border-[1px] border-indigo-600 rounded-full px-4 py-[6px] 
      text-indigo-600 font-semibold"
    >
      {text}
    </div>
  );
}

export default Tag;
